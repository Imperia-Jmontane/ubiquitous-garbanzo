using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyApp.Application.CodeAnalysis.DTOs;

namespace MyApp.Domain.CodeAnalysis
{
    public interface ICodeGraphRepository
    {
        Task BeginTransactionAsync(CancellationToken ct);
        Task CommitTransactionAsync(CancellationToken ct);
        Task RollbackTransactionAsync(CancellationToken ct);

        Task<long> CreateRepositorySnapshotAsync(string repositoryId, string repositoryPath, string? commitSha, string? branchName, CancellationToken ct);
        Task<IndexedRepository?> GetRepositorySnapshotAsync(string repositoryId, CancellationToken ct);
        Task UpdateRepositoryStatusAsync(long snapshotId, IndexingStatus status, string? errorMessage, int filesIndexed, int symbolsCollected, int referencesCollected, TimeSpan duration, CancellationToken ct);

        Task<long> RecordFileAsync(long snapshotId, string relativePath, string language, string? fileHash, CancellationToken ct);
        Task<long?> GetFileIdAsync(long snapshotId, string relativePath, CancellationToken ct);
        Task<bool> IsFileChangedAsync(long snapshotId, string relativePath, string fileHash, CancellationToken ct);

        long RecordNode(long snapshotId, string serializedName, string? displayName, CSharpSymbolKind kind, long? parentNodeId, int? accessibility, bool isStatic, bool isAbstract, bool isVirtual, bool isOverride, bool isExtensionMethod, bool isAsync);
        long GetOrCreateNodeId(long snapshotId, string serializedName);
        long? TryGetNodeId(long snapshotId, string serializedName);

        long RecordExternalNode(long snapshotId, string serializedName, string? displayName, CSharpSymbolKind kind);

        long RecordEdge(long snapshotId, long sourceNodeId, long targetNodeId, CSharpReferenceKind kind);

        void RecordSourceLocation(long nodeId, long fileId, int startLine, int startColumn, int endLine, int endColumn, int startOffset, int endOffset, LocationType locationType);
        void RecordOccurrence(long elementId, long fileId, int startLine, int startColumn, int endLine, int endColumn, int startOffset, int endOffset);

        Task<GraphData> GetGraphDataAsync(GraphQueryOptions options, CancellationToken ct);
        Task<List<ReferenceLocation>> GetSymbolReferencesAsync(long symbolId, CancellationToken ct);
        Task<List<SymbolSearchResult>> SearchSymbolsAsync(string repositoryId, string query, int limit, CancellationToken ct);
    }
}
