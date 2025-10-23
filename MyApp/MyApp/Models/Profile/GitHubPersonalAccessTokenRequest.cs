using System.ComponentModel.DataAnnotations;

namespace MyApp.Models.Profile
{
    public sealed class GitHubPersonalAccessTokenRequest
    {
        [Required(ErrorMessage = "El token personal es obligatorio.")]
        [MinLength(40, ErrorMessage = "El token debe tener al menos 40 caracteres.")]
        [MaxLength(200, ErrorMessage = "El token no puede superar los 200 caracteres.")]
        public string Token { get; set; } = string.Empty;
    }
}
