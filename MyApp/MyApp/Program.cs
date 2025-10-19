using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation.AspNetCore;
using FluentValidation;
using MediatR;
using MyApp.Data;
using MyApp.Application.Abstractions;
using MyApp.Application.GitHubOAuth.Commands.LinkGitHubAccount;
using MyApp.Application.GitHubOAuth.Configuration;
using MyApp.Application.Configuration;
using MyApp.Infrastructure.Git;
using MyApp.Infrastructure.GitHub;
using MyApp.Infrastructure.Persistence;
using MyApp.Infrastructure.Secrets;
using MyApp.Infrastructure.Time;
using MyApp.Domain.Scopes;
using MyApp.Middleware;
using Serilog;
using Serilog.Context;
using Microsoft.OpenApi.Models;

namespace MyApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            builder.Host.UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext());

            string appDataPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
            Directory.CreateDirectory(appDataPath);

            string dataProtectionPath = Path.Combine(appDataPath, "keys");
            Directory.CreateDirectory(dataProtectionPath);
            DirectoryInfo dataProtectionDirectory = new DirectoryInfo(dataProtectionPath);
            builder.Services.AddDataProtection().PersistKeysToFileSystem(dataProtectionDirectory);

            string configuredRepositoryRoot = builder.Configuration.GetValue<string>("Repositories:RootPath") ?? "temp";
            string repositoryRootPath = Path.IsPathRooted(configuredRepositoryRoot)
                ? configuredRepositoryRoot
                : Path.Combine(builder.Environment.ContentRootPath, configuredRepositoryRoot);
            Directory.CreateDirectory(repositoryRootPath);
            builder.Services.Configure<RepositoryStorageOptions>(options =>
            {
                options.RootPath = repositoryRootPath;
            });

            string rawConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("The connection string 'DefaultConnection' was not found.");
            string resolvedConnectionString = rawConnectionString.Contains("{AppDataPath}", StringComparison.OrdinalIgnoreCase)
                ? rawConnectionString.Replace("{AppDataPath}", appDataPath, StringComparison.OrdinalIgnoreCase)
                : rawConnectionString;

            string configuredProvider = builder.Configuration.GetValue<string>("Database:Provider") ?? string.Empty;
            bool providerForSqlite = string.Equals(configuredProvider, "Sqlite", StringComparison.OrdinalIgnoreCase);
            bool providerForSqlServer = string.Equals(configuredProvider, "SqlServer", StringComparison.OrdinalIgnoreCase);

            bool looksLikeSqlite = resolvedConnectionString.IndexOf(".db", StringComparison.OrdinalIgnoreCase) >= 0
                || resolvedConnectionString.IndexOf(".sqlite", StringComparison.OrdinalIgnoreCase) >= 0
                || resolvedConnectionString.IndexOf("mode=memory", StringComparison.OrdinalIgnoreCase) >= 0;

            bool looksLikeSqlServer = resolvedConnectionString.IndexOf("server=", StringComparison.OrdinalIgnoreCase) >= 0
                || (resolvedConnectionString.IndexOf("data source=", StringComparison.OrdinalIgnoreCase) >= 0 && !looksLikeSqlite)
                || resolvedConnectionString.IndexOf("initial catalog=", StringComparison.OrdinalIgnoreCase) >= 0;

            bool useSqlServer = providerForSqlServer || (!providerForSqlite && looksLikeSqlServer);

            // Add services to the container.
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
            {
                if (useSqlServer)
                {
                    options.UseSqlServer(resolvedConnectionString);
                }
                else
                {
                    options.UseSqlite(resolvedConnectionString);
                }
            });
            builder.Services.AddControllersWithViews();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                OpenApiSecurityScheme oauthScheme = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Description = "GitHub OAuth2 flow requiring repo, workflow, and read:user scopes.",
                    Flows = new OpenApiOAuthFlows
                    {
                        AuthorizationCode = new OpenApiOAuthFlow
                        {
                            AuthorizationUrl = new Uri("https://github.com/login/oauth/authorize"),
                            TokenUrl = new Uri("https://github.com/login/oauth/access_token"),
                            Scopes = new Dictionary<string, string>
                            {
                                { GitHubScopes.Repo, "Full access to repositories for cloning." },
                                { GitHubScopes.Workflow, "Manage GitHub workflows." },
                                { GitHubScopes.ReadUser, "Read basic user profile information." }
                            }
                        }
                    }
                };

                OpenApiInfo apiInfo = new OpenApiInfo
                {
                    Title = "MyApp API",
                    Version = "v1",
                    Description = "Endpoints for GitHub authentication and repository ingestion workflows."
                };

                options.SwaggerDoc("v1", apiInfo);
                options.EnableAnnotations();
                options.AddSecurityDefinition("GitHubOAuth", oauthScheme);
                OpenApiSecurityScheme securitySchemeReference = new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "GitHubOAuth"
                    }
                };
                OpenApiSecurityRequirement requirement = new OpenApiSecurityRequirement
                {
                    { securitySchemeReference, new List<string> { GitHubScopes.Repo, GitHubScopes.Workflow, GitHubScopes.ReadUser } }
                };
                options.AddSecurityRequirement(requirement);
            });
            builder.Services.AddFluentValidationAutoValidation();
            builder.Services.AddFluentValidationClientsideAdapters();
            builder.Services.AddMediatR(typeof(LinkGitHubAccountCommand));
            builder.Services.AddValidatorsFromAssemblyContaining<LinkGitHubAccountCommandValidator>();
            builder.Services.AddSingleton<ISystemClock, SystemClock>();
            builder.Services.AddSingleton<Meter>(_ => new Meter("MyApp.GitHubOAuth"));
            builder.Services.AddSingleton<IWritableSecretStore, DataProtectedWritableSecretStore>();
            builder.Services.AddSingleton<ISecretProvider, ConfigurationSecretProvider>();
            builder.Services.AddScoped<IGitCredentialStore, GitCredentialStore>();
            builder.Services.AddSingleton<IGitHubOAuthSettingsProvider, GitHubOAuthSettingsProvider>();
            builder.Services.Configure<GitHubOAuthOptions>(builder.Configuration.GetSection("GitHubOAuth"));
            builder.Services.Configure<BootstrapOptions>(builder.Configuration.GetSection("Bootstrap"));
            builder.Services.AddSingleton<ILocalRepositoryService, LocalRepositoryService>();
            builder.Services.AddSingleton<IRepositoryCloneCoordinator, RepositoryCloneCoordinator>();
            builder.Services.AddHttpClient<IGitHubOAuthClient, GitHubOAuthClient>();
            builder.Services.AddHttpClient<IGitHubUserProfileClient, GitHubUserProfileClient>(client =>
            {
                client.BaseAddress = new Uri("https://api.github.com/", UriKind.Absolute);
                client.DefaultRequestHeaders.UserAgent.Clear();
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MyApp", "1.0"));
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            });
            builder.Services.AddScoped<IUserExternalLoginRepository, UserExternalLoginRepository>();
            builder.Services.AddScoped<IGitHubOAuthStateRepository, GitHubOAuthStateRepository>();
            builder.Services.AddScoped<IAuditTrailRepository, AuditTrailRepository>();

            WebApplication app = builder.Build();

            using (IServiceScope migrationScope = app.Services.CreateScope())
            {
                ApplicationDbContext applicationDbContext = migrationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                applicationDbContext.Database.Migrate();
            }

            app.UseSerilogRequestLogging();

            app.Use(async (context, next) =>
            {
                string state = context.TraceIdentifier;
                string userId = context.User != null && context.User.Identity != null && context.User.Identity.IsAuthenticated
                    ? context.User.Identity.Name ?? "unknown"
                    : "anonymous";

                IDisposable stateProperty = LogContext.PushProperty("state", state);
                IDisposable userProperty = LogContext.PushProperty("userId", userId);

                try
                {
                    await next.Invoke();
                }
                finally
                {
                    stateProperty.Dispose();
                    userProperty.Dispose();
                }
            });

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "MyApp API v1");
                options.OAuthAppName("MyApp GitHub OAuth");
            });

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseMiddleware<GitHubOAuthBootstrapMiddleware>();

            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllers();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            app.Run();
        }
    }
}
