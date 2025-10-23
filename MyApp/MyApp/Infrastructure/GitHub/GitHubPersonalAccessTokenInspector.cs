using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyApp.Application.Abstractions;
using MyApp.Application.GitHubPersonalAccessToken;
using MyApp.Application.GitHubPersonalAccessToken.Models;

namespace MyApp.Infrastructure.GitHub
{
    public sealed class GitHubPersonalAccessTokenInspector : IGitHubPersonalAccessTokenInspector
    {
        private readonly HttpClient httpClient;
        private readonly ILogger<GitHubPersonalAccessTokenInspector> logger;

        public GitHubPersonalAccessTokenInspector(HttpClient httpClient, ILogger<GitHubPersonalAccessTokenInspector> logger)
        {
            this.httpClient = httpClient;
            this.logger = logger;
        }

        public async Task<GitHubPersonalAccessTokenInspectionResult> InspectAsync(string token, IReadOnlyCollection<string> requiredScopes, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException("The token cannot be null or whitespace.", nameof(token));
            }

            try
            {
                GitHubPersonalAccessTokenInspectionResult inspection = await InspectInternalAsync(token, requiredScopes, cancellationToken);
                return inspection;
            }
            catch (HttpRequestException exception)
            {
                logger.LogError(exception, "Failed to contact GitHub while validating the personal access token.");
                List<string> emptyList = new List<string>();
                return new GitHubPersonalAccessTokenInspectionResult(false, false, false, false, null, emptyList, new List<string>(requiredScopes), new List<string> { "No se pudo contactar a GitHub para validar el token. Intenta nuevamente." }, "No se pudo contactar a GitHub para validar el token.");
            }
        }

        private async Task<GitHubPersonalAccessTokenInspectionResult> InspectInternalAsync(string token, IReadOnlyCollection<string> requiredScopes, CancellationToken cancellationToken)
        {
            using HttpRequestMessage userRequest = CreateRequest(HttpMethod.Get, "user", token);
            HttpResponseMessage userResponse = await httpClient.SendAsync(userRequest, cancellationToken);

            if (userResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                string unauthorizedMessage = await userResponse.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("GitHub rejected the personal access token with 401 Unauthorized. Body: {Body}", unauthorizedMessage);
                List<string> emptyList = new List<string>();
                List<string> warnings = new List<string> { "GitHub rechazó el token. Genera uno nuevo y vuelve a intentarlo." };
                return new GitHubPersonalAccessTokenInspectionResult(false, false, false, false, null, emptyList, new List<string>(requiredScopes), warnings, "GitHub rechazó el token. Verifica que no haya expirado y que lo copiaste completo.");
            }

            string? login = null;
            List<string> grantedPermissions = new List<string>();
            List<string> missingPermissions = new List<string>();
            List<string> warningsList = new List<string>();
            bool isFineGrained = false;

            if (!userResponse.IsSuccessStatusCode)
            {
                string errorBody = await userResponse.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("Unexpected status code {StatusCode} while validating the GitHub personal access token. Body: {Body}", (int)userResponse.StatusCode, errorBody);
                List<string> emptyList = new List<string>();
                warningsList.Add("GitHub devolvió un estado inesperado al validar el token. Vuelve a intentarlo más tarde.");
                return new GitHubPersonalAccessTokenInspectionResult(false, false, false, false, null, emptyList, new List<string>(requiredScopes), warningsList, "GitHub devolvió un estado inesperado al validar el token.");
            }

            string responseContent = await userResponse.Content.ReadAsStringAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(responseContent))
            {
                try
                {
                    using JsonDocument document = JsonDocument.Parse(responseContent);
                    if (document.RootElement.TryGetProperty("login", out JsonElement loginElement) && loginElement.ValueKind == JsonValueKind.String)
                    {
                        login = loginElement.GetString();
                    }
                }
                catch (JsonException exception)
                {
                    logger.LogWarning(exception, "Failed to parse the GitHub user response when validating the token.");
                }
            }

            string scopesHeader = GetHeaderValue(userResponse.Headers, "X-OAuth-Scopes");
            if (!string.IsNullOrWhiteSpace(scopesHeader))
            {
                string[] parsedScopes = scopesHeader.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string scope in parsedScopes)
                {
                    string trimmedScope = scope.Trim();
                    if (!string.IsNullOrEmpty(trimmedScope))
                    {
                        grantedPermissions.Add(trimmedScope);
                    }
                }

                foreach (string requiredScope in requiredScopes)
                {
                    if (!ContainsIgnoreCase(grantedPermissions, requiredScope))
                    {
                        missingPermissions.Add(requiredScope);
                    }
                }
            }
            else
            {
                isFineGrained = true;
                warningsList.Add("GitHub no expone los scopes para tokens fine-grained. Verifica manualmente que el token tenga permisos de Contents: read y Actions.");
            }

            bool repositoryAccessConfirmed = await CheckRepositoryAccessAsync(token, warningsList, cancellationToken);

            if (!repositoryAccessConfirmed)
            {
                if (!ContainsIgnoreCase(missingPermissions, "repo"))
                {
                    missingPermissions.Add("repo");
                }
            }

            bool hasRequiredPermissions = missingPermissions.Count == 0 && repositoryAccessConfirmed;

            return new GitHubPersonalAccessTokenInspectionResult(true, hasRequiredPermissions, isFineGrained, repositoryAccessConfirmed, login, grantedPermissions, missingPermissions, warningsList, null);
        }

        private static HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl, string token)
        {
            HttpRequestMessage request = new HttpRequestMessage(method, relativeUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("token", token);
            return request;
        }

        private async Task<bool> CheckRepositoryAccessAsync(string token, List<string> warnings, CancellationToken cancellationToken)
        {
            using HttpRequestMessage repositoriesRequest = CreateRequest(HttpMethod.Get, "user/repos?per_page=1&visibility=private", token);
            HttpResponseMessage repositoriesResponse = await httpClient.SendAsync(repositoriesRequest, cancellationToken);
            if (repositoriesResponse.IsSuccessStatusCode)
            {
                return true;
            }

            string message = await repositoriesResponse.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Failed to confirm private repository access. Status: {StatusCode}. Body: {Body}", (int)repositoriesResponse.StatusCode, message);

            if (repositoriesResponse.StatusCode == HttpStatusCode.Forbidden || repositoriesResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                warnings.Add("GitHub no permitió listar repos privados con este token. Asegúrate de conceder Contents: read o el scope repo.");
            }
            else
            {
                warnings.Add(string.Concat("No se pudo comprobar el acceso a repos privados (estado ", ((int)repositoriesResponse.StatusCode).ToString(CultureInfo.InvariantCulture), ")."));
            }

            return false;
        }

        private static string GetHeaderValue(HttpResponseHeaders headers, string name)
        {
            if (headers.TryGetValues(name, out IEnumerable<string>? values))
            {
                foreach (string value in values)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }

            return string.Empty;
        }

        private static bool ContainsIgnoreCase(IEnumerable<string> source, string value)
        {
            foreach (string item in source)
            {
                if (string.Equals(item, value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
