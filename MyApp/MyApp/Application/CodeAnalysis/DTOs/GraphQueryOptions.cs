using System.Collections.Generic;
using MyApp.Domain.CodeAnalysis;

namespace MyApp.Application.CodeAnalysis.DTOs
{
    public sealed class GraphQueryOptions
    {
        public string RepositoryId { get; set; } = string.Empty;

        public int MaxDepth { get; set; } = 2;

        public int MaxNodes { get; set; } = 100;

        public int MaxEdges { get; set; } = 500;

        public bool IncludeMembers { get; set; }

        public long? RootNodeId { get; set; }

        public string? NamespaceFilter { get; set; }

        public List<CSharpSymbolKind>? SymbolKindFilter { get; set; }
    }
}
