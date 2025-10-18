using System;
using MyApp.Application.Abstractions;

namespace MyApp.Application.GitHubOAuth.DTOs
{
    public sealed class GitHubAccountDetailsDto
    {
        public GitHubAccountDetailsDto(bool isLinked, string provider, string? externalUserId, DateTimeOffset? expiresAt, bool supportsRefresh, GitHubUserProfileInfo? profile, string? profileFetchError)
        {
            IsLinked = isLinked;
            Provider = provider ?? string.Empty;
            ExternalUserId = externalUserId ?? string.Empty;
            ExpiresAt = expiresAt;
            SupportsRefresh = supportsRefresh;
            Profile = profile;
            ProfileFetchError = profileFetchError ?? string.Empty;
        }

        public bool IsLinked { get; }

        public string Provider { get; }

        public string ExternalUserId { get; }

        public DateTimeOffset? ExpiresAt { get; }

        public bool SupportsRefresh { get; }

        public GitHubUserProfileInfo? Profile { get; }

        public string ProfileFetchError { get; }
    }
}
