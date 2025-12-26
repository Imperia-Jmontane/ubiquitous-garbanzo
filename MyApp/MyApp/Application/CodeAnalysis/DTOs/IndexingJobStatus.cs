using System;
using MyApp.Domain.CodeAnalysis;

namespace MyApp.Application.CodeAnalysis.DTOs
{
    public sealed class IndexingJobStatus
    {
        public string RepositoryId { get; set; } = string.Empty;

        public IndexingStatus Status { get; set; }

        public DateTime? StartedAtUtc { get; set; }

        public DateTime? CompletedAtUtc { get; set; }

        public int? FilesIndexed { get; set; }

        public int? TotalFiles { get; set; }

        public string? CurrentFile { get; set; }

        public string? ErrorMessage { get; set; }

        public int ProgressPercent { get; set; }
    }
}
