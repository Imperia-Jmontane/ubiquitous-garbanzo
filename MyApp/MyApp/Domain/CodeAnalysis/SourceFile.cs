using System;
using System.Collections.Generic;

namespace MyApp.Domain.CodeAnalysis
{
    public sealed class SourceFile
    {
        public long Id { get; set; }

        public string Path { get; set; } = string.Empty;

        public string Language { get; set; } = "csharp";

        public string? FileHash { get; set; }

        public DateTime? ModificationTime { get; set; }

        public bool IsIndexed { get; set; }

        public bool IsComplete { get; set; }

        public int? LineCount { get; set; }

        public long RepositorySnapshotId { get; set; }

        public ICollection<SourceLocation> SourceLocations { get; set; } = new List<SourceLocation>();

        public IndexedRepository RepositorySnapshot { get; set; } = null!;
    }
}
