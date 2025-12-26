using System;
using System.Collections.Generic;

namespace MyApp.CodeAnalysis.Domain.CodeAnalysis
{
    public sealed class IndexedRepository
    {
        public long Id { get; set; }

        public string RepositoryId { get; set; } = string.Empty;

        public string RepositoryPath { get; set; } = string.Empty;

        public string? CommitSha { get; set; }

        public string? BranchName { get; set; }

        public DateTime IndexedAtUtc { get; set; }

        public IndexingStatus Status { get; set; }

        public string? ErrorMessage { get; set; }

        public int FilesIndexed { get; set; }

        public int SymbolsCollected { get; set; }

        public int ReferencesCollected { get; set; }

        public TimeSpan IndexingDuration { get; set; }

        public ICollection<SourceFile> Files { get; set; } = new List<SourceFile>();

        public ICollection<CodeNode> Nodes { get; set; } = new List<CodeNode>();

        public ICollection<CodeEdge> Edges { get; set; } = new List<CodeEdge>();
    }
}
