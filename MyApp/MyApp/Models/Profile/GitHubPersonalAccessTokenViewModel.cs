using System.Collections.Generic;

namespace MyApp.Models.Profile
{
    public sealed class GitHubPersonalAccessTokenViewModel
    {
        public bool IsConfigured { get; set; }

        public string GenerationUrl { get; set; } = string.Empty;

        public List<string> RequiredPermissions { get; set; } = new List<string>();
    }
}
