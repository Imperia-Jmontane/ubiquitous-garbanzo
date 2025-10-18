using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyApp.Models.Bootstrap
{
    public sealed class GitHubBootstrapViewModel
    {
        public bool IsConfigured { get; set; }

        public string ClientIdPreview { get; set; } = string.Empty;

        public List<string> Scopes { get; set; } = new List<string>();

        public GitHubBootstrapFormModel Form { get; set; } = new GitHubBootstrapFormModel();

        public string? ErrorMessage { get; set; }

        public bool Success { get; set; }
    }

    public sealed class GitHubBootstrapFormModel
    {
        [Required(ErrorMessage = "El Client ID es obligatorio.")]
        [StringLength(200, MinimumLength = 5, ErrorMessage = "El Client ID debe tener entre 5 y 200 caracteres.")]
        public string ClientId { get; set; } = string.Empty;

        [Required(ErrorMessage = "El Client Secret es obligatorio.")]
        [StringLength(400, MinimumLength = 20, ErrorMessage = "El Client Secret debe tener al menos 20 caracteres.")]
        public string ClientSecret { get; set; } = string.Empty;

        [Required(ErrorMessage = "El password temporal es obligatorio.")]
        [StringLength(200, MinimumLength = 6, ErrorMessage = "El password temporal debe tener entre 6 y 200 caracteres.")]
        public string SetupPassword { get; set; } = string.Empty;
    }
}
