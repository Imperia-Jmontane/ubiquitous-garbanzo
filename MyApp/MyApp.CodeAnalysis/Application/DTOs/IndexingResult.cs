using System;
using System.Collections.Generic;

namespace MyApp.CodeAnalysis.Application.DTOs
{
    public sealed class IndexingResult
    {
        public long SnapshotId { get; set; }

        public int FilesIndexed { get; set; }

        public int SymbolsCollected { get; set; }

        public int ReferencesCollected { get; set; }

        public TimeSpan Duration { get; set; }

        public List<string> Errors { get; set; } = new List<string>();

        public bool PartialSuccess { get; set; }
    }
}
