using System.Collections.Generic;

namespace MyApp.Domain.CodeAnalysis
{
    public sealed class CodeNode : CodeElement
    {
        public CSharpSymbolKind Type { get; set; }

        public string SerializedName { get; set; } = string.Empty;

        public string? DisplayName { get; set; }

        public string? NormalizedName { get; set; }

        public int? Accessibility { get; set; }

        public bool IsStatic { get; set; }

        public bool IsAbstract { get; set; }

        public bool IsVirtual { get; set; }

        public bool IsOverride { get; set; }

        public bool IsExtensionMethod { get; set; }

        public bool IsAsync { get; set; }

        public long? ParentNodeId { get; set; }

        public long? RepositorySnapshotId { get; set; }

        public ICollection<CodeEdge> OutgoingEdges { get; set; } = new List<CodeEdge>();

        public ICollection<CodeEdge> IncomingEdges { get; set; } = new List<CodeEdge>();

        public ICollection<Occurrence> Occurrences { get; set; } = new List<Occurrence>();

        public CodeNode? ParentNode { get; set; }

        public ICollection<CodeNode> ChildNodes { get; set; } = new List<CodeNode>();

        public IndexedRepository? RepositorySnapshot { get; set; }
    }
}
