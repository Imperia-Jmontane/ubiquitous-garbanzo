using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyApp.Application.Authentication.Interfaces;
using MyApp.Application.Authentication.Models;
using MyApp.Application.Common.Interfaces;
using MyApp.Domain.ValueObjects;
using System.Text.Json.Serialization;

namespace MyApp.Infrastructure.Authentication
{
    public sealed class GitHubOAuthClient : IGitHubOAuthClient
    {
        private readonly HttpClient httpClient;
        private readonly GitHubOAuthOptions options;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly ILogger<GitHubOAuthClient> logger;
        private readonly JsonSerializerOptions serializerOptions;

        public GitHubOAuthClient(
            HttpClient httpClient,
            IOptions<GitHubOAuthOptions> options,
            IDateTimeProvider dateTimeProvider,
            ILogger<GitHubOAuthClient> logger)
        {
            this.httpClient = httpClient;
            this.options = options.Value;
            this.dateTimeProvider = dateTimeProvider;
            this.logger = logger;
            serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            if (!this.httpClient.DefaultRequestHeaders.UserAgent.Any())
            {
                this.httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MyApp", "1.0"));
            }
        }

        public GitHubAuthorizationInfo CreateAuthorizationInfo(string state, string redirectUri)
        {
            EnsureRedirectUriIsAllowed(redirectUri);

            string scopeParameter = string.Join(" ", options.Scopes);
            StringBuilder builder = new StringBuilder();
            builder.Append(options.AuthorizationEndpoint);
            builder.Append("?client_id=");
            builder.Append(Uri.EscapeDataString(options.ClientId));
            builder.Append("&redirect_uri=");
            builder.Append(Uri.EscapeDataString(redirectUri));
            builder.Append("&scope=");
            builder.Append(Uri.EscapeDataString(scopeParameter));
            builder.Append("&state=");
            builder.Append(Uri.EscapeDataString(state));

            string authorizationUrl = builder.ToString();

            return new GitHubAuthorizationInfo(authorizationUrl, state, options.Scopes.ToList());
        }

        public async Task<GitHubOAuthSession> ExchangeCodeForTokenAsync(string code, string redirectUri, CancellationToken cancellationToken)
        {
            EnsureRedirectUriIsAllowed(redirectUri);

            logger.LogInformation("Requesting GitHub token with authorization code.");

            using HttpRequestMessage tokenRequest = BuildTokenRequest(new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "code", code },
                { "redirect_uri", redirectUri }
            });

            using HttpResponseMessage tokenResponse = await httpClient.SendAsync(tokenRequest, cancellationToken);
            string responseBody = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!tokenResponse.IsSuccessStatusCode)
            {
                logger.LogError("GitHub token exchange failed with status {StatusCode}. Response: {Response}", tokenResponse.StatusCode, responseBody);
                throw new InvalidOperationException("GitHub token exchange failed.");
            }

            GitHubTokenResponse? tokenData = JsonSerializer.Deserialize<GitHubTokenResponse>(responseBody, serializerOptions);

            if (tokenData == null)
            {
                throw new InvalidOperationException("GitHub token response was empty.");
            }

            GitHubToken token = CreateTokenFromResponse(tokenData);
            GitHubIdentity identity = await FetchIdentityAsync(token.AccessToken, cancellationToken);

            return new GitHubOAuthSession(identity, token);
        }

        public async Task<GitHubOAuthSession> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
        {
            logger.LogInformation("Refreshing GitHub access token.");

            using HttpRequestMessage tokenRequest = BuildTokenRequest(new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "refresh_token", refreshToken }
            });

            using HttpResponseMessage tokenResponse = await httpClient.SendAsync(tokenRequest, cancellationToken);
            string responseBody = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!tokenResponse.IsSuccessStatusCode)
            {
                logger.LogError("GitHub token refresh failed with status {StatusCode}. Response: {Response}", tokenResponse.StatusCode, responseBody);
                throw new InvalidOperationException("GitHub token refresh failed.");
            }

            GitHubTokenResponse? tokenData = JsonSerializer.Deserialize<GitHubTokenResponse>(responseBody, serializerOptions);

            if (tokenData == null)
            {
                throw new InvalidOperationException("GitHub refresh response was empty.");
            }

            GitHubToken token = CreateTokenFromResponse(tokenData);
            GitHubIdentity identity = await FetchIdentityAsync(token.AccessToken, cancellationToken);

            return new GitHubOAuthSession(identity, token);
        }

        private HttpRequestMessage BuildTokenRequest(IDictionary<string, string> parameters)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, options.TokenEndpoint);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            string credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Concat(options.ClientId, ":", options.ClientSecret)));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            request.Content = new FormUrlEncodedContent(parameters);

            return request;
        }

        private GitHubToken CreateTokenFromResponse(GitHubTokenResponse response)
        {
            DateTimeOffset issuedAt = dateTimeProvider.UtcNow;
            DateTimeOffset? expiresAt = response.ExpiresIn.HasValue ? issuedAt.AddSeconds(response.ExpiresIn.Value) : null;

            IReadOnlyCollection<string> scopes = ParseScopes(response.Scope);

            GitHubToken token = new GitHubToken(response.AccessToken, response.RefreshToken ?? string.Empty, issuedAt, expiresAt, scopes);

            return token;
        }

        private IReadOnlyCollection<string> ParseScopes(string scopeValue)
        {
            if (string.IsNullOrWhiteSpace(scopeValue))
            {
                return options.Scopes.ToList();
            }

            string[] separators = new[] { ",", " " };
            string[] segments = scopeValue.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            List<string> scopes = new List<string>();

            foreach (string segment in segments)
            {
                scopes.Add(segment.Trim());
            }

            return scopes;
        }

        private async Task<GitHubIdentity> FetchIdentityAsync(string accessToken, CancellationToken cancellationToken)
        {
            using HttpRequestMessage userRequest = new HttpRequestMessage(HttpMethod.Get, options.UserEndpoint);
            userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            userRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using HttpResponseMessage userResponse = await httpClient.SendAsync(userRequest, cancellationToken);
            string responseBody = await userResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!userResponse.IsSuccessStatusCode)
            {
                logger.LogError("GitHub user profile request failed with status {StatusCode}. Response: {Response}", userResponse.StatusCode, responseBody);
                throw new InvalidOperationException("GitHub user profile request failed.");
            }

            GitHubUserResponse? user = JsonSerializer.Deserialize<GitHubUserResponse>(responseBody, serializerOptions);

            if (user == null)
            {
                throw new InvalidOperationException("GitHub user profile response was empty.");
            }

            GitHubIdentity identity = new GitHubIdentity(user.Id.ToString(), user.Login, user.Name ?? user.Login, user.AvatarUrl ?? string.Empty);
            return identity;
        }

        private void EnsureRedirectUriIsAllowed(string redirectUri)
        {
            if (options.AllowedRedirectUris.Count > 0 && !options.AllowedRedirectUris.Any(uri => string.Equals(uri, redirectUri, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("The redirect URI is not configured for GitHub OAuth.");
            }
        }

        private sealed class GitHubTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = string.Empty;

            [JsonPropertyName("refresh_token")]
            public string? RefreshToken { get; set; }

            [JsonPropertyName("expires_in")]
            public int? ExpiresIn { get; set; }

            [JsonPropertyName("scope")]
            public string Scope { get; set; } = string.Empty;
        }

        private sealed class GitHubUserResponse
        {
            public long Id { get; set; }

            public string Login { get; set; } = string.Empty;

            public string? Name { get; set; }

            [JsonPropertyName("avatar_url")]
            public string? AvatarUrl { get; set; }
        }
    }
}
