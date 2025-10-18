using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyApp.Application.GitHubOAuth.Configuration;

namespace MyApp.Middleware
{
    public sealed class GitHubOAuthBootstrapMiddleware
    {
        private static readonly HashSet<string> StaticFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".css",
            ".js",
            ".png",
            ".jpg",
            ".jpeg",
            ".gif",
            ".svg",
            ".woff",
            ".woff2",
            ".ttf",
            ".ico",
            ".map"
        };

        private readonly RequestDelegate next;

        public GitHubOAuthBootstrapMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task InvokeAsync(HttpContext context, IGitHubOAuthSettingsProvider settingsProvider)
        {
            GitHubOAuthSettings settings = await settingsProvider.GetSettingsAsync(context.RequestAborted);
            if (!settings.IsConfigured)
            {
                HttpRequest request = context.Request;
                if (IsApiRequest(request) && !IsBootstrapApiRequest(request))
                {
                    await WriteBootstrapRequiredResponseAsync(context);
                    return;
                }

                if (ShouldRedirect(request))
                {
                    string redirectTarget = "/bootstrap/github";
                    context.Response.Redirect(redirectTarget);
                    return;
                }
            }

            await next(context);
        }

        private static bool ShouldRedirect(HttpRequest request)
        {
            if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
            {
                return false;
            }

            PathString path = request.Path;
            if (path.Equals("/bootstrap/github", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (IsBootstrapApiRequest(request))
            {
                return false;
            }

            if (path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWithSegments("/healthz", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (IsStaticAsset(path))
            {
                return false;
            }

            return true;
        }

        private static bool IsStaticAsset(PathString path)
        {
            string value = path.Value ?? string.Empty;
            int index = value.LastIndexOf('.');
            if (index < 0 || index == value.Length - 1)
            {
                return false;
            }

            string extension = value.Substring(index);
            return StaticFileExtensions.Contains(extension);
        }

        private static bool IsApiRequest(HttpRequest request)
        {
            return request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBootstrapApiRequest(HttpRequest request)
        {
            return request.Path.StartsWithSegments("/api/bootstrap/github", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task WriteBootstrapRequiredResponseAsync(HttpContext context)
        {
            ProblemDetails problem = new ProblemDetails
            {
                Title = "GitHub OAuth no configurado",
                Detail = "Debes registrar los secretos de GitHub antes de utilizar esta funcionalidad.",
                Status = StatusCodes.Status503ServiceUnavailable
            };

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";
            string payload = JsonSerializer.Serialize(problem);
            await context.Response.WriteAsync(payload);
        }
    }
}
