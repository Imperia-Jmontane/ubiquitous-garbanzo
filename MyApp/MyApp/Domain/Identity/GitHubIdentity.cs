using System;

namespace MyApp.Domain.Identity
{
    public sealed class GitHubIdentity
    {
        public GitHubIdentity(Guid id, string login, string displayName)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The GitHub user identifier cannot be empty.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(login))
            {
                throw new ArgumentException("The GitHub login cannot be null or whitespace.", nameof(login));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("The GitHub display name cannot be null or whitespace.", nameof(displayName));
            }

            Id = id;
            Login = login;
            DisplayName = displayName;
        }

        public Guid Id { get; }

        public string Login { get; }

        public string DisplayName { get; }
    }
}
