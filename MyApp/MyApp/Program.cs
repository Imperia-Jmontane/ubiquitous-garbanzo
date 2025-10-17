using System;
using Microsoft.EntityFrameworkCore;
using MyApp.Data;
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
