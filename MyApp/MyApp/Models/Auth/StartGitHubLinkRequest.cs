using System;

namespace MyApp.Models.Auth
{
    public sealed class StartGitHubLinkRequest
    {
        public Guid UserId { get; set; }

        public string RedirectUri { get; set; } = string.Empty;
    }
}
