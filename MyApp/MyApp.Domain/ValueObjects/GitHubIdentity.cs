using System;

namespace MyApp.Domain.ValueObjects
{
    public sealed class GitHubIdentity
    {
        public GitHubIdentity(string accountId, string login, string displayName, string avatarUrl)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                throw new ArgumentException("The GitHub account identifier is required.", nameof(accountId));
            }

            if (string.IsNullOrWhiteSpace(login))
            {
                throw new ArgumentException("The GitHub login is required.", nameof(login));
            }

            AccountId = accountId;
            Login = login;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? login : displayName;
            AvatarUrl = avatarUrl ?? string.Empty;
        }

        public string AccountId { get; }

        public string Login { get; }

        public string DisplayName { get; }

        public string AvatarUrl { get; }
    }
}
