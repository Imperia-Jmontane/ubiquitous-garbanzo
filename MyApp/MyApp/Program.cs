using System;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using MyApp.Application.Authentication.Commands;
using MyApp.Application.Authentication.Interfaces;
using MyApp.Application.Authentication.Validators;
using MyApp.Application.Common.Interfaces;
using MyApp.Infrastructure.Authentication;
using MyApp.Infrastructure.Metrics;
using MyApp.Infrastructure.Persistence;
using MyApp.Infrastructure.Persistence.Repositories;
using MyApp.Infrastructure.Security;
using MyApp.Infrastructure.Services;
using Serilog;

namespace MyApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            ConfigureSerilog(builder);

            string defaultConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("The connection string 'DefaultConnection' was not found.");

            builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(defaultConnectionString));

            builder.Services.AddDataProtection();
            builder.Services.Configure<GitHubOAuthOptions>(builder.Configuration.GetSection("GitHubOAuth"));
            builder.Services.Configure<GitCredentialStoreOptions>(builder.Configuration.GetSection("GitCredentialStore"));

            RegisterSecretRepository(builder);

            builder.Services.AddScoped<IGitCredentialStore, GitCredentialStore>();
            builder.Services.AddScoped<IUserExternalLoginRepository, UserExternalLoginRepository>();
            builder.Services.AddScoped<IGitHubAccountLinkRepository, GitHubAccountLinkRepository>();
            builder.Services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
            builder.Services.AddSingleton<IStateGenerator, SecureStateGenerator>();
            builder.Services.AddSingleton<IGitHubLinkMetrics, GitHubLinkMetricsRecorder>();

            builder.Services.AddHttpClient<IGitHubOAuthClient, GitHubOAuthClient>();

            builder.Services.AddMediatR(typeof(LinkGitHubAccountCommand));
            builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(MyApp.Application.Common.Behaviors.ValidationBehavior<,>));
            builder.Services.AddValidatorsFromAssemblyContaining<LinkGitHubAccountCommandValidator>();

            builder.Services.AddControllers();
            builder.Services.AddControllersWithViews();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                OpenApiInfo apiInfo = new OpenApiInfo
                {
                    Title = "MyApp Authentication API",
                    Version = "v1",
                    Description = "Endpoints to manage GitHub OAuth linking."
                };

                options.SwaggerDoc("v1", apiInfo);
                options.EnableAnnotations();
            });

            WebApplication app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseSerilogRequestLogging();

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            app.MapControllers();

            app.Run();
        }

        private static void ConfigureSerilog(WebApplicationBuilder builder)
        {
            LoggerConfiguration loggerConfiguration = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "MyApp")
                .WriteTo.Console();

            Log.Logger = loggerConfiguration.CreateLogger();
            builder.Host.UseSerilog();
        }

        private static void RegisterSecretRepository(WebApplicationBuilder builder)
        {
            string? keyVaultUri = builder.Configuration["KeyVault:Uri"];

            if (!string.IsNullOrWhiteSpace(keyVaultUri))
            {
                builder.Services.AddSingleton(provider =>
                {
                    SecretClientOptions options = new SecretClientOptions();
                    return new SecretClient(new Uri(keyVaultUri), new DefaultAzureCredential(), options);
                });

                builder.Services.AddSingleton<ISecretRepository, KeyVaultSecretRepository>();
            }
            else
            {
                builder.Services.AddSingleton<ISecretRepository, InMemorySecretRepository>();
            }
        }
    }
}
