using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyApp.Application.Abstractions;

namespace MyApp.Infrastructure.GitHub
{
    public sealed class GitHubUserProfileClient : IGitHubUserProfileClient
    {
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient httpClient;
        private readonly ILogger<GitHubUserProfileClient> logger;

        public GitHubUserProfileClient(HttpClient httpClient, ILogger<GitHubUserProfileClient> logger)
        {
            this.httpClient = httpClient;
            this.logger = logger;
        }

        public async Task<GitHubUserProfileInfo> GetProfileAsync(string accessToken, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new ArgumentException("The access token cannot be null or whitespace.", nameof(accessToken));
            }

            GitHubUserResponse userResponse = await GetUserAsync(accessToken, cancellationToken);
            IReadOnlyList<string> organizations = await GetOrganizationsAsync(accessToken, cancellationToken);

            string login = string.IsNullOrWhiteSpace(userResponse.Login) ? "unknown" : userResponse.Login;
            GitHubUserProfileInfo profile = new GitHubUserProfileInfo(
                login,
                userResponse.Name,
                userResponse.Email,
                userResponse.AvatarUrl,
                userResponse.ProfileUrl,
                organizations);

            return profile;
        }

        private async Task<GitHubUserResponse> GetUserAsync(string accessToken, CancellationToken cancellationToken)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "user");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);

            string content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("GitHub user endpoint returned {StatusCode}. Body: {Body}", response.StatusCode, content);
                throw new InvalidOperationException("Failed to obtain the GitHub user profile.");
            }

            GitHubUserResponse? payload = JsonSerializer.Deserialize<GitHubUserResponse>(content, SerializerOptions);
            if (payload == null)
            {
                throw new InvalidOperationException("GitHub user response payload was empty.");
            }

            return payload;
        }

        private async Task<IReadOnlyList<string>> GetOrganizationsAsync(string accessToken, CancellationToken cancellationToken)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "user/orgs");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);

            string content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Unauthorized)
            {
                logger.LogInformation("GitHub organizations endpoint denied access with status {StatusCode}.", response.StatusCode);
                return new List<string>().AsReadOnly();
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("GitHub organizations endpoint returned {StatusCode}. Body: {Body}", response.StatusCode, content);
                return new List<string>().AsReadOnly();
            }

            List<string> organizations = new List<string>();
            GitHubOrganizationResponse[]? payload = JsonSerializer.Deserialize<GitHubOrganizationResponse[]>(content, SerializerOptions);
            if (payload != null)
            {
                foreach (GitHubOrganizationResponse organization in payload)
                {
                    if (!string.IsNullOrWhiteSpace(organization.Login))
                    {
                        organizations.Add(organization.Login);
                    }
                }
            }

            return organizations.AsReadOnly();
        }

        private sealed class GitHubUserResponse
        {
            public string Login { get; set; } = string.Empty;

            public string? Name { get; set; }

            public string? Email { get; set; }

            [JsonPropertyName("avatar_url")]
            public string? AvatarUrl { get; set; }

            [JsonPropertyName("html_url")]
            public string? ProfileUrl { get; set; }
        }

        private sealed class GitHubOrganizationResponse
        {
            public string Login { get; set; } = string.Empty;
        }
    }
}
