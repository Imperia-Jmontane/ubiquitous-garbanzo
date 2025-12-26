namespace MyApp.Domain.CodeAnalysis
{
    public sealed class IndexingError
    {
        public long Id { get; set; }

        public long RepositorySnapshotId { get; set; }

        public string? Message { get; set; }

        public bool IsFatal { get; set; }

        public long? FileId { get; set; }

        public int? Line { get; set; }

        public int? Column { get; set; }

        public IndexedRepository RepositorySnapshot { get; set; } = null!;

        public SourceFile? File { get; set; }
    }
}
