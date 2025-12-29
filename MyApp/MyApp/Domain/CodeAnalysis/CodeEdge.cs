using System.Collections.Generic;
using MyApp.CodeAnalysis.Abstractions;

namespace MyApp.Domain.CodeAnalysis
{
    public sealed class CodeEdge : CodeElement
    {
        public CSharpReferenceKind Type { get; set; }

        public long SourceNodeId { get; set; }

        public long TargetNodeId { get; set; }

        public long? RepositorySnapshotId { get; set; }

        public CodeNode SourceNode { get; set; } = null!;

        public CodeNode TargetNode { get; set; } = null!;

        public ICollection<Occurrence> Occurrences { get; set; } = new List<Occurrence>();

        public IndexedRepository? RepositorySnapshot { get; set; }
    }
}
