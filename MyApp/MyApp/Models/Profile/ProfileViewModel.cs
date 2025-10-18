using System;
using System.Collections.Generic;

namespace MyApp.Models.Profile
{
    public sealed class ProfileViewModel
    {
        public bool GitHubConfigured { get; set; }

        public string ClientIdPreview { get; set; } = string.Empty;

        public List<string> GitHubScopes { get; set; } = new List<string>();

        public Guid UserId { get; set; }

        public string GitHubRedirectUri { get; set; } = string.Empty;
    }
}
