using System.Collections.Generic;

namespace MyApp.Domain.CodeAnalysis
{
    public sealed class SourceLocation
    {
        public long Id { get; set; }

        public long FileId { get; set; }

        public int StartLine { get; set; }

        public int StartColumn { get; set; }

        public int EndLine { get; set; }

        public int EndColumn { get; set; }

        public int StartOffset { get; set; }

        public int EndOffset { get; set; }

        public LocationType Type { get; set; }

        public SourceFile File { get; set; } = null!;

        public ICollection<Occurrence> Occurrences { get; set; } = new List<Occurrence>();
    }
}
