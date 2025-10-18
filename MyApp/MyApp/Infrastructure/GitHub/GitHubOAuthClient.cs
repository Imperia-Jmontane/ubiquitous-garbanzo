using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyApp.Application.Abstractions;
using MyApp.Application.GitHubOAuth.Configuration;
using MyApp.Application.GitHubOAuth.Models;

namespace MyApp.Infrastructure.GitHub
{
    public sealed class GitHubOAuthClient : IGitHubOAuthClient
    {
        private readonly HttpClient httpClient;
        private readonly IGitCredentialStore credentialStore;
        private readonly GitHubOAuthOptions options;
        private readonly ILogger<GitHubOAuthClient> logger;

        public GitHubOAuthClient(HttpClient httpClient, IGitCredentialStore credentialStore, IOptions<GitHubOAuthOptions> options, ILogger<GitHubOAuthClient> logger)
        {
            this.httpClient = httpClient;
            this.credentialStore = credentialStore;
            this.options = options.Value;
            this.logger = logger;
        }

        public async Task<GitHubOAuthTokenResponse> ExchangeCodeAsync(GitHubCodeExchangeRequest request, CancellationToken cancellationToken)
        {
            return await SendRequestAsync(
                credentials => JsonSerializer.Serialize(new
                {
                    client_id = credentials.ClientId,
                    client_secret = credentials.ClientSecret,
                    code = request.Code,
                    redirect_uri = request.RedirectUri,
                    state = request.State,
                    grant_type = "authorization_code"
                }),
                cancellationToken);
        }

        public async Task<GitHubOAuthTokenResponse> RefreshTokenAsync(GitHubTokenRefreshRequest request, CancellationToken cancellationToken)
        {
            return await SendRequestAsync(
                credentials => JsonSerializer.Serialize(new
                {
                    client_id = credentials.ClientId,
                    client_secret = credentials.ClientSecret,
                    refresh_token = request.RefreshToken,
                    grant_type = "refresh_token"
                }),
                cancellationToken);
        }

        private async Task<GitHubOAuthTokenResponse> SendRequestAsync(Func<GitHubOAuthClientCredentials, string> payloadFactory, CancellationToken cancellationToken)
        {
            GitHubOAuthClientCredentials credentials = await credentialStore.GetClientCredentialsAsync(cancellationToken);
            using HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, options.TokenEndpoint);
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            string rawCredentials = string.Concat(credentials.ClientId, ":", credentials.ClientSecret);
            string encodedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawCredentials));
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", encodedCredentials);
            string payload = payloadFactory(credentials);
            httpRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await httpClient.SendAsync(httpRequest, cancellationToken);
            string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("GitHub OAuth request failed with status {StatusCode}. Body: {Body}", response.StatusCode, responseContent);
                throw new InvalidOperationException("GitHub OAuth request failed.");
            }

            GitHubOAuthTokenPayload? payloadModel = JsonSerializer.Deserialize<GitHubOAuthTokenPayload>(responseContent);
            if (payloadModel == null)
            {
                throw new InvalidOperationException("GitHub OAuth response was empty.");
            }

            return payloadModel.ToResponse();
        }

        private sealed class GitHubOAuthTokenPayload
        {
            public string access_token { get; set; } = string.Empty;

            public string refresh_token { get; set; } = string.Empty;

            public int expires_in { get; set; }

            public string token_type { get; set; } = string.Empty;

            public string scope { get; set; } = string.Empty;

            public string? node_id { get; set; }

            public GitHubOAuthTokenResponse ToResponse()
            {
                return new GitHubOAuthTokenResponse(access_token, refresh_token, expires_in <= 0 ? 3600 : expires_in, token_type, scope, node_id);
            }
        }
    }
}
