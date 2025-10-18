using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace MyApp.Models.Bootstrap
{
    public sealed class GitHubBootstrapRequest
    {
        [Required]
        [StringLength(200)]
        public string ClientId { get; set; } = string.Empty;

        [Required]
        [StringLength(400, MinimumLength = 20)]
        public string ClientSecret { get; set; } = string.Empty;

        [Required]
        [StringLength(200, MinimumLength = 6)]
        public string SetupPassword { get; set; } = string.Empty;
    }

    public sealed class GitHubBootstrapStatusResponse
    {
        public bool IsConfigured { get; set; }

        public string ClientIdPreview { get; set; } = string.Empty;

        public List<string> Scopes { get; set; } = new List<string>();
    }
}
