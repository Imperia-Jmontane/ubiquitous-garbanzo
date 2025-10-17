using System;

namespace MyApp.Models.Auth
{
    public sealed class RefreshGitHubTokenRequest
    {
        public Guid UserId { get; set; }
    }
}
