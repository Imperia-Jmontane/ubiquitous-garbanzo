using System;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation.AspNetCore;
using FluentValidation;
using MediatR;
using MyApp.Data;
using MyApp.Application.Abstractions;
using MyApp.Application.GitHubOAuth.Commands.LinkGitHubAccount;
using MyApp.Infrastructure.GitHub;
using MyApp.Infrastructure.Persistence;
using MyApp.Infrastructure.Secrets;
using MyApp.Infrastructure.Time;
using Serilog;
using Serilog.Context;

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

            string defaultConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("The connection string 'DefaultConnection' was not found.");

            // Add services to the container.
            builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(defaultConnectionString));
            builder.Services.AddControllersWithViews();
            builder.Services.AddFluentValidationAutoValidation();
            builder.Services.AddFluentValidationClientsideAdapters();
            builder.Services.AddMediatR(typeof(LinkGitHubAccountCommand));
            builder.Services.AddValidatorsFromAssemblyContaining<LinkGitHubAccountCommandValidator>();
            builder.Services.AddSingleton<ISystemClock, SystemClock>();
            builder.Services.AddSingleton<Meter>(_ => new Meter("MyApp.GitHubOAuth"));
            builder.Services.AddSingleton<ISecretProvider, ConfigurationSecretProvider>();
            builder.Services.AddScoped<IGitCredentialStore, GitCredentialStore>();
            builder.Services.Configure<GitHubOAuthOptions>(builder.Configuration.GetSection("GitHubOAuth"));
            builder.Services.AddHttpClient<IGitHubOAuthClient, GitHubOAuthClient>();
            builder.Services.AddScoped<IUserExternalLoginRepository, UserExternalLoginRepository>();

            WebApplication app = builder.Build();

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

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            app.Run();
        }
    }
}
