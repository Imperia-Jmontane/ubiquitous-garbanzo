using System;

namespace MyApp.Models.Auth
{
    public sealed class CompleteGitHubLinkRequest
    {
        public Guid UserId { get; set; }

        public string Code { get; set; } = string.Empty;

        public string State { get; set; } = string.Empty;

        public string RedirectUri { get; set; } = string.Empty;
    }
}
