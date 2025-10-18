using System;

namespace MyApp.Application.Configuration
{
    public sealed class BootstrapOptions
    {
        public string SetupPassword { get; set; } = string.Empty;

        public Guid AuditUserId { get; set; } = Guid.Empty;
    }
}
