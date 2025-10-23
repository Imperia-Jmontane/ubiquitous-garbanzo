using System.Collections.Generic;

namespace MyApp.Models.Profile
{
    public sealed class GitHubPersonalAccessTokenValidationViewModel
    {
        public bool TokenAccepted { get; set; }

        public bool HasRequiredPermissions { get; set; }

        public bool IsFineGrained { get; set; }

        public bool RepositoryAccessConfirmed { get; set; }

        public string? Login { get; set; }

        public string? FailureReason { get; set; }

        public List<string> GrantedPermissions { get; set; } = new List<string>();

        public List<string> MissingPermissions { get; set; } = new List<string>();

        public List<string> Warnings { get; set; } = new List<string>();
    }
}
