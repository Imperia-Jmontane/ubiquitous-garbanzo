using System;
using System.ComponentModel.DataAnnotations;

namespace MyApp.Models.GitHubAuth
{
    public sealed class GitHubOAuthStartRequest
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        [MaxLength(500)]
        [Url]
        public string RedirectUri { get; set; } = string.Empty;
    }
}
