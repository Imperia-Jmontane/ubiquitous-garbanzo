namespace MyApp.CodeAnalysis.Abstractions
{
    /// <summary>
    /// Minimal repository interface for use by symbol collectors during indexing.
    /// This interface contains only synchronous methods needed during syntax tree walking.
    /// </summary>
    public interface ISymbolCollectorRepository
    {
        /// <summary>
        /// Records a code symbol node.
        /// </summary>
        long RecordNode(
            long snapshotId,
            string serializedName,
            string? displayName,
            CSharpSymbolKind kind,
            long? parentNodeId,
            int? accessibility,
            bool isStatic,
            bool isAbstract,
            bool isVirtual,
            bool isOverride,
            bool isExtensionMethod,
            bool isAsync);

        /// <summary>
        /// Gets or creates a node ID for the given serialized name.
        /// Used for creating references to symbols that may not have been visited yet.
        /// </summary>
        long GetOrCreateNodeId(long snapshotId, string serializedName);

        /// <summary>
        /// Records an edge (relationship) between two nodes.
        /// </summary>
        long RecordEdge(long snapshotId, long sourceNodeId, long targetNodeId, CSharpReferenceKind kind);

        /// <summary>
        /// Records the source location of a symbol.
        /// </summary>
        void RecordSourceLocation(
            long nodeId,
            long fileId,
            int startLine,
            int startColumn,
            int endLine,
            int endColumn,
            int startOffset,
            int endOffset,
            LocationType locationType);
    }
}
