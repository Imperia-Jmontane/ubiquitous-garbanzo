using System;
using System.ComponentModel.DataAnnotations;

namespace MyApp.Models.GitHubAuth
{
    public sealed class GitHubOAuthCallbackRequest
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string State { get; set; } = string.Empty;
    }
}
