using System;
using System.Collections.Generic;

namespace MyApp.Models.Profile
{
    public sealed class GitHubAccountViewModel
    {
        public bool IsLinked { get; set; }

        public string Provider { get; set; } = string.Empty;

        public string ExternalUserId { get; set; } = string.Empty;

        public DateTimeOffset? ExpiresAt { get; set; }

        public bool SupportsRefresh { get; set; }

        public bool DetailsAvailable { get; set; }

        public string? Login { get; set; }

        public string? Name { get; set; }

        public string? Email { get; set; }

        public string? AvatarUrl { get; set; }

        public string? ProfileUrl { get; set; }

        public List<string> Organizations { get; set; } = new List<string>();

        public string? Error { get; set; }
    }
}
