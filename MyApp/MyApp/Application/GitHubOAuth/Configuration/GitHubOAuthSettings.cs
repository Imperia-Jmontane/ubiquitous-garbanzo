using System;
using System.Collections.Generic;

namespace MyApp.Application.GitHubOAuth.Configuration
{
    public sealed class GitHubOAuthSettings
    {
        public GitHubOAuthSettings(
            string clientId,
            string authorizationEndpoint,
            string tokenEndpoint,
            string userInformationEndpoint,
            string callbackPath,
            IReadOnlyList<string> scopes,
            bool isConfigured)
        {
            if (clientId == null)
            {
                throw new ArgumentNullException(nameof(clientId));
            }

            if (authorizationEndpoint == null)
            {
                throw new ArgumentNullException(nameof(authorizationEndpoint));
            }

            if (tokenEndpoint == null)
            {
                throw new ArgumentNullException(nameof(tokenEndpoint));
            }

            if (userInformationEndpoint == null)
            {
                throw new ArgumentNullException(nameof(userInformationEndpoint));
            }

            if (callbackPath == null)
            {
                throw new ArgumentNullException(nameof(callbackPath));
            }

            if (scopes == null)
            {
                throw new ArgumentNullException(nameof(scopes));
            }

            ClientId = clientId;
            AuthorizationEndpoint = authorizationEndpoint;
            TokenEndpoint = tokenEndpoint;
            UserInformationEndpoint = userInformationEndpoint;
            CallbackPath = callbackPath;
            Scopes = scopes;
            IsConfigured = isConfigured;
        }

        public string ClientId { get; }

        public string AuthorizationEndpoint { get; }

        public string TokenEndpoint { get; }

        public string UserInformationEndpoint { get; }

        public string CallbackPath { get; }

        public IReadOnlyList<string> Scopes { get; }

        public bool IsConfigured { get; }
    }
}
