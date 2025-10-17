using System;

namespace MyApp.Models.Auth
{
    public sealed class CompleteGitHubLinkResponse
    {
        public Guid UserId { get; set; }

        public string GitHubLogin { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string AvatarUrl { get; set; } = string.Empty;

        public bool CanClone { get; set; }
    }
}
