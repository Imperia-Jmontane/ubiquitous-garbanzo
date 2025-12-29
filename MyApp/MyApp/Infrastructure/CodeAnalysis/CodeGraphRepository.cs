using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyApp.Application.CodeAnalysis.DTOs;
using MyApp.CodeAnalysis.Abstractions;
using MyApp.Data;
using MyApp.Domain.CodeAnalysis;

namespace MyApp.Infrastructure.CodeAnalysis
{
    public sealed class CodeGraphRepository : ICodeGraphRepository
    {
        private readonly ApplicationDbContext dbContext;
        private readonly Dictionary<string, long> nodeIdCache;
        private readonly Dictionary<string, long> fileIdCache;
        private IDbContextTransaction? transaction;

        public CodeGraphRepository(ApplicationDbContext dbContext)
        {
            this.dbContext = dbContext;
            nodeIdCache = new Dictionary<string, long>(StringComparer.Ordinal);
            fileIdCache = new Dictionary<string, long>(StringComparer.Ordinal);
        }

        public async Task BeginTransactionAsync(CancellationToken ct)
        {
            if (transaction != null)
            {
                return;
            }

            transaction = await dbContext.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        }

        public async Task CommitTransactionAsync(CancellationToken ct)
        {
            if (transaction == null)
            {
                return;
            }

            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            await transaction.DisposeAsync().ConfigureAwait(false);
            transaction = null;
            ClearCaches();
        }

        public async Task RollbackTransactionAsync(CancellationToken ct)
        {
            if (transaction == null)
            {
                return;
            }

            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            await transaction.DisposeAsync().ConfigureAwait(false);
            transaction = null;
            ClearCaches();
        }

        public async Task<long> CreateRepositorySnapshotAsync(string repositoryId, string repositoryPath, string? commitSha, string? branchName, CancellationToken ct)
        {
            IndexedRepository snapshot = new IndexedRepository
            {
                RepositoryId = repositoryId,
                RepositoryPath = repositoryPath,
                CommitSha = commitSha,
                BranchName = branchName,
                IndexedAtUtc = DateTime.UtcNow,
                Status = IndexingStatus.Queued,
                ErrorMessage = null,
                FilesIndexed = 0,
                SymbolsCollected = 0,
                ReferencesCollected = 0,
                IndexingDuration = TimeSpan.Zero
            };

            dbContext.IndexedRepositories.Add(snapshot);
            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

            return snapshot.Id;
        }

        public async Task<IndexedRepository?> GetRepositorySnapshotAsync(string repositoryId, CancellationToken ct)
        {
            return await dbContext.IndexedRepositories
                .AsNoTracking()
                .FirstOrDefaultAsync(repository => repository.RepositoryId == repositoryId, ct)
                .ConfigureAwait(false);
        }

        public async Task UpdateRepositoryStatusAsync(long snapshotId, IndexingStatus status, string? errorMessage, int filesIndexed, int symbolsCollected, int referencesCollected, TimeSpan duration, CancellationToken ct)
        {
            IndexedRepository? snapshot = await dbContext.IndexedRepositories
                .FirstOrDefaultAsync(repository => repository.Id == snapshotId, ct)
                .ConfigureAwait(false);

            if (snapshot == null)
            {
                return;
            }

            snapshot.Status = status;
            snapshot.ErrorMessage = errorMessage;
            snapshot.FilesIndexed = filesIndexed;
            snapshot.SymbolsCollected = symbolsCollected;
            snapshot.ReferencesCollected = referencesCollected;
            snapshot.IndexingDuration = duration;
            snapshot.IndexedAtUtc = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        public async Task<long> RecordFileAsync(long snapshotId, string relativePath, string language, string? fileHash, CancellationToken ct)
        {
            string cacheKey = BuildFileCacheKey(snapshotId, relativePath);

            if (fileIdCache.TryGetValue(cacheKey, out long cachedId))
            {
                return cachedId;
            }

            SourceFile? existingFile = await dbContext.SourceFiles
                .FirstOrDefaultAsync(file => file.RepositorySnapshotId == snapshotId && file.Path == relativePath, ct)
                .ConfigureAwait(false);

            if (existingFile != null)
            {
                fileIdCache[cacheKey] = existingFile.Id;
                return existingFile.Id;
            }

            SourceFile fileEntry = new SourceFile
            {
                RepositorySnapshotId = snapshotId,
                Path = relativePath,
                Language = language,
                FileHash = fileHash,
                IsIndexed = true,
                IsComplete = true,
                ModificationTime = DateTime.UtcNow
            };

            dbContext.SourceFiles.Add(fileEntry);
            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

            fileIdCache[cacheKey] = fileEntry.Id;
            return fileEntry.Id;
        }

        public async Task<long?> GetFileIdAsync(long snapshotId, string relativePath, CancellationToken ct)
        {
            string cacheKey = BuildFileCacheKey(snapshotId, relativePath);

            if (fileIdCache.TryGetValue(cacheKey, out long cachedId))
            {
                return cachedId;
            }

            SourceFile? fileEntry = await dbContext.SourceFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(file => file.RepositorySnapshotId == snapshotId && file.Path == relativePath, ct)
                .ConfigureAwait(false);

            if (fileEntry == null)
            {
                return null;
            }

            fileIdCache[cacheKey] = fileEntry.Id;
            return fileEntry.Id;
        }

        public async Task<bool> IsFileChangedAsync(long snapshotId, string relativePath, string fileHash, CancellationToken ct)
        {
            SourceFile? fileEntry = await dbContext.SourceFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(file => file.RepositorySnapshotId == snapshotId && file.Path == relativePath, ct)
                .ConfigureAwait(false);

            if (fileEntry == null)
            {
                return true;
            }

            if (fileEntry.FileHash == null)
            {
                return true;
            }

            return !string.Equals(fileEntry.FileHash, fileHash, StringComparison.OrdinalIgnoreCase);
        }

        public long RecordNode(long snapshotId, string serializedName, string? displayName, CSharpSymbolKind kind, long? parentNodeId, int? accessibility, bool isStatic, bool isAbstract, bool isVirtual, bool isOverride, bool isExtensionMethod, bool isAsync)
        {
            string cacheKey = BuildNodeCacheKey(snapshotId, serializedName);

            if (nodeIdCache.TryGetValue(cacheKey, out long cachedId))
            {
                return cachedId;
            }

            CodeNode? existingNode = dbContext.CodeNodes
                .FirstOrDefault(node => node.RepositorySnapshotId == snapshotId && node.SerializedName == serializedName);

            if (existingNode != null)
            {
                nodeIdCache[cacheKey] = existingNode.Id;
                return existingNode.Id;
            }

            CodeNode newNode = new CodeNode
            {
                RepositorySnapshotId = snapshotId,
                SerializedName = serializedName,
                DisplayName = displayName,
                NormalizedName = displayName == null ? null : displayName.ToLowerInvariant(),
                Type = kind,
                ParentNodeId = parentNodeId,
                Accessibility = accessibility,
                IsStatic = isStatic,
                IsAbstract = isAbstract,
                IsVirtual = isVirtual,
                IsOverride = isOverride,
                IsExtensionMethod = isExtensionMethod,
                IsAsync = isAsync
            };

            dbContext.CodeNodes.Add(newNode);
            dbContext.SaveChanges();

            nodeIdCache[cacheKey] = newNode.Id;
            return newNode.Id;
        }

        public long GetOrCreateNodeId(long snapshotId, string serializedName)
        {
            long? existingId = TryGetNodeId(snapshotId, serializedName);

            if (existingId.HasValue)
            {
                return existingId.Value;
            }

            return RecordNode(snapshotId, serializedName, null, CSharpSymbolKind.Unknown, null, null, false, false, false, false, false, false);
        }

        public long? TryGetNodeId(long snapshotId, string serializedName)
        {
            string cacheKey = BuildNodeCacheKey(snapshotId, serializedName);

            if (nodeIdCache.TryGetValue(cacheKey, out long cachedId))
            {
                return cachedId;
            }

            CodeNode? existingNode = dbContext.CodeNodes
                .AsNoTracking()
                .FirstOrDefault(node => node.RepositorySnapshotId == snapshotId && node.SerializedName == serializedName);

            if (existingNode == null)
            {
                return null;
            }

            nodeIdCache[cacheKey] = existingNode.Id;
            return existingNode.Id;
        }

        public long RecordExternalNode(long snapshotId, string serializedName, string? displayName, CSharpSymbolKind kind)
        {
            string cacheKey = BuildNodeCacheKey(snapshotId, serializedName);

            if (nodeIdCache.TryGetValue(cacheKey, out long cachedId))
            {
                return cachedId;
            }

            CodeNode? existingNode = dbContext.CodeNodes
                .FirstOrDefault(node => node.RepositorySnapshotId == snapshotId && node.SerializedName == serializedName);

            if (existingNode != null)
            {
                nodeIdCache[cacheKey] = existingNode.Id;
                return existingNode.Id;
            }

            string normalizedName = displayName == null ? serializedName.ToLowerInvariant() : displayName.ToLowerInvariant();

            CodeNode externalNode = new CodeNode
            {
                RepositorySnapshotId = snapshotId,
                SerializedName = serializedName,
                DisplayName = displayName,
                NormalizedName = normalizedName,
                Type = kind
            };

            dbContext.CodeNodes.Add(externalNode);
            dbContext.SaveChanges();

            nodeIdCache[cacheKey] = externalNode.Id;
            return externalNode.Id;
        }

        public long RecordEdge(long snapshotId, long sourceNodeId, long targetNodeId, CSharpReferenceKind kind)
        {
            CodeEdge edge = new CodeEdge
            {
                RepositorySnapshotId = snapshotId,
                SourceNodeId = sourceNodeId,
                TargetNodeId = targetNodeId,
                Type = kind
            };

            dbContext.CodeEdges.Add(edge);
            dbContext.SaveChanges();

            return edge.Id;
        }

        public void RecordSourceLocation(long nodeId, long fileId, int startLine, int startColumn, int endLine, int endColumn, int startOffset, int endOffset, LocationType locationType)
        {
            SourceLocation location = new SourceLocation
            {
                FileId = fileId,
                StartLine = startLine,
                StartColumn = startColumn,
                EndLine = endLine,
                EndColumn = endColumn,
                StartOffset = startOffset,
                EndOffset = endOffset,
                Type = locationType
            };

            Occurrence occurrence = new Occurrence
            {
                ElementId = nodeId,
                SourceLocation = location
            };

            dbContext.SourceLocations.Add(location);
            dbContext.Occurrences.Add(occurrence);
            dbContext.SaveChanges();
        }

        public void RecordOccurrence(long elementId, long fileId, int startLine, int startColumn, int endLine, int endColumn, int startOffset, int endOffset)
        {
            SourceLocation location = new SourceLocation
            {
                FileId = fileId,
                StartLine = startLine,
                StartColumn = startColumn,
                EndLine = endLine,
                EndColumn = endColumn,
                StartOffset = startOffset,
                EndOffset = endOffset,
                Type = LocationType.Reference
            };

            Occurrence occurrence = new Occurrence
            {
                ElementId = elementId,
                SourceLocation = location
            };

            dbContext.SourceLocations.Add(location);
            dbContext.Occurrences.Add(occurrence);
            dbContext.SaveChanges();
        }

        public async Task<GraphData> GetGraphDataAsync(GraphQueryOptions options, CancellationToken ct)
        {
            IndexedRepository? snapshot = await dbContext.IndexedRepositories
                .AsNoTracking()
                .FirstOrDefaultAsync(repository => repository.RepositoryId == options.RepositoryId, ct)
                .ConfigureAwait(false);

            if (snapshot == null)
            {
                return new GraphData
                {
                    Nodes = new List<GraphNode>(),
                    Edges = new List<GraphEdge>(),
                    HasMore = false
                };
            }

            int maxNodes = options.MaxNodes <= 0 ? 100 : options.MaxNodes;
            int maxEdges = options.MaxEdges <= 0 ? 500 : options.MaxEdges;

            List<CodeNode> nodes = new List<CodeNode>();
            List<CodeEdge> edges = new List<CodeEdge>();
            HashSet<long> nodeIds = new HashSet<long>();
            HashSet<long> edgeIds = new HashSet<long>();
            bool hasMore = false;

            if (options.RootNodeId.HasValue)
            {
                CodeNode? rootNode = await dbContext.CodeNodes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(node => node.Id == options.RootNodeId.Value, ct)
                    .ConfigureAwait(false);

                if (rootNode == null)
                {
                    return new GraphData
                    {
                        Nodes = new List<GraphNode>(),
                        Edges = new List<GraphEdge>(),
                        HasMore = false
                    };
                }

                nodes.Add(rootNode);
                nodeIds.Add(rootNode.Id);

                Queue<NodeTraversal> traversalQueue = new Queue<NodeTraversal>();
                traversalQueue.Enqueue(new NodeTraversal(rootNode.Id, 0));

                while (traversalQueue.Count > 0)
                {
                    NodeTraversal traversal = traversalQueue.Dequeue();

                    if (edgeIds.Count >= maxEdges)
                    {
                        hasMore = true;
                        break;
                    }

                    List<CodeEdge> relatedEdges = await dbContext.CodeEdges
                        .AsNoTracking()
                        .Where(edge => edge.RepositorySnapshotId == snapshot.Id && (edge.SourceNodeId == traversal.NodeId || edge.TargetNodeId == traversal.NodeId))
                        .ToListAsync(ct)
                        .ConfigureAwait(false);

                    foreach (CodeEdge edge in relatedEdges)
                    {
                        if (edgeIds.Count >= maxEdges)
                        {
                            hasMore = true;
                            break;
                        }

                        if (!edgeIds.Add(edge.Id))
                        {
                            continue;
                        }

                        edges.Add(edge);

                        long neighborId = edge.SourceNodeId == traversal.NodeId ? edge.TargetNodeId : edge.SourceNodeId;

                        if (nodeIds.Contains(neighborId))
                        {
                            continue;
                        }

                        if (traversal.Depth + 1 > options.MaxDepth)
                        {
                            continue;
                        }

                        if (nodeIds.Count >= maxNodes)
                        {
                            hasMore = true;
                            continue;
                        }

                        CodeNode? neighborNode = await dbContext.CodeNodes
                            .AsNoTracking()
                            .FirstOrDefaultAsync(node => node.Id == neighborId, ct)
                            .ConfigureAwait(false);

                        if (neighborNode == null)
                        {
                            continue;
                        }

                        if (!IsNodeAllowed(neighborNode, options))
                        {
                            continue;
                        }

                        nodes.Add(neighborNode);
                        nodeIds.Add(neighborNode.Id);
                        traversalQueue.Enqueue(new NodeTraversal(neighborNode.Id, traversal.Depth + 1));
                    }

                    if (hasMore)
                    {
                        break;
                    }
                }
            }
            else
            {
                IQueryable<CodeNode> query = dbContext.CodeNodes
                    .AsNoTracking()
                    .Where(node => node.RepositorySnapshotId == snapshot.Id);

                if (!options.IncludeMembers)
                {
                    query = query.Where(node => node.ParentNodeId == null);
                }

                if (!string.IsNullOrWhiteSpace(options.NamespaceFilter))
                {
                    string prefix = options.NamespaceFilter;
                    query = query.Where(node => node.SerializedName.StartsWith(prefix));
                }

                if (options.SymbolKindFilter != null && options.SymbolKindFilter.Count > 0)
                {
                    List<CSharpSymbolKind> kinds = options.SymbolKindFilter;
                    query = query.Where(node => kinds.Contains(node.Type));
                }

                List<CodeNode> queriedNodes = await query
                    .OrderBy(node => node.Id)
                    .Take(maxNodes + 1)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                if (queriedNodes.Count > maxNodes)
                {
                    hasMore = true;
                    queriedNodes = queriedNodes.Take(maxNodes).ToList();
                }

                nodes = queriedNodes;
                nodeIds = new HashSet<long>(nodes.Select(node => node.Id));

                if (nodeIds.Count > 0)
                {
                    List<CodeEdge> queriedEdges = await dbContext.CodeEdges
                        .AsNoTracking()
                        .Where(edge => edge.RepositorySnapshotId == snapshot.Id
                            && nodeIds.Contains(edge.SourceNodeId)
                            && nodeIds.Contains(edge.TargetNodeId))
                        .OrderBy(edge => edge.Id)
                        .Take(maxEdges + 1)
                        .ToListAsync(ct)
                        .ConfigureAwait(false);

                    if (queriedEdges.Count > maxEdges)
                    {
                        hasMore = true;
                        queriedEdges = queriedEdges.Take(maxEdges).ToList();
                    }

                    edges = queriedEdges;
                }
            }

            List<long> nodeIdList = nodes.Select(node => node.Id).ToList();
            Dictionary<long, NodeLocation> nodeLocations = await GetNodeLocationLookupAsync(nodeIdList, LocationType.Definition, ct).ConfigureAwait(false);

            List<GraphNode> graphNodes = new List<GraphNode>();
            foreach (CodeNode node in nodes)
            {
                NodeLocation? location;
                nodeLocations.TryGetValue(node.Id, out location);

                GraphNode graphNode = new GraphNode
                {
                    Id = node.Id,
                    SerializedName = node.SerializedName,
                    DisplayName = node.DisplayName,
                    Type = node.Type.ToString(),
                    FilePath = location == null ? null : location.FilePath,
                    Line = location == null ? null : location.Line,
                    Column = location == null ? null : location.Column,
                    ParentId = node.ParentNodeId
                };

                graphNodes.Add(graphNode);
            }

            List<GraphEdge> graphEdges = new List<GraphEdge>();
            foreach (CodeEdge edge in edges)
            {
                GraphEdge graphEdge = new GraphEdge
                {
                    Id = edge.Id,
                    SourceNodeId = edge.SourceNodeId,
                    TargetNodeId = edge.TargetNodeId,
                    Type = edge.Type.ToString()
                };

                graphEdges.Add(graphEdge);
            }

            return new GraphData
            {
                Nodes = graphNodes,
                Edges = graphEdges,
                HasMore = hasMore
            };
        }

        public async Task<List<ReferenceLocation>> GetSymbolReferencesAsync(long symbolId, CancellationToken ct)
        {
            IQueryable<ReferenceLocation> query = dbContext.Occurrences
                .AsNoTracking()
                .Where(occurrence => occurrence.ElementId == symbolId)
                .Join(dbContext.SourceLocations.AsNoTracking(),
                    occurrence => occurrence.SourceLocationId,
                    location => location.Id,
                    (occurrence, location) => new OccurrenceLocation
                    {
                        ElementId = occurrence.ElementId,
                        FileId = location.FileId,
                        StartLine = location.StartLine,
                        StartColumn = location.StartColumn,
                        EndLine = location.EndLine,
                        EndColumn = location.EndColumn,
                        Type = location.Type
                    })
                .Where(location => location.Type == LocationType.Reference)
                .Join(dbContext.SourceFiles.AsNoTracking(),
                    location => location.FileId,
                    file => file.Id,
                    (location, file) => new ReferenceLocation
                    {
                        FilePath = file.Path,
                        Line = location.StartLine,
                        Column = location.StartColumn,
                        EndLine = location.EndLine,
                        EndColumn = location.EndColumn,
                        Context = null
                    });

            List<ReferenceLocation> locations = await query.ToListAsync(ct).ConfigureAwait(false);
            return locations;
        }

        public async Task<List<SymbolSearchResult>> SearchSymbolsAsync(string repositoryId, string query, int limit, CancellationToken ct)
        {
            IndexedRepository? snapshot = await dbContext.IndexedRepositories
                .AsNoTracking()
                .FirstOrDefaultAsync(repository => repository.RepositoryId == repositoryId, ct)
                .ConfigureAwait(false);

            if (snapshot == null)
            {
                return new List<SymbolSearchResult>();
            }

            string normalizedQuery = query.Trim().ToLowerInvariant();

            IQueryable<CodeNode> nodesQuery = dbContext.CodeNodes
                .AsNoTracking()
                .Where(node => node.RepositorySnapshotId == snapshot.Id && node.NormalizedName != null && node.NormalizedName.Contains(normalizedQuery));

            nodesQuery = nodesQuery
                .OrderBy(node => node.NormalizedName != null && node.NormalizedName == normalizedQuery ? 0 : node.NormalizedName != null && node.NormalizedName.StartsWith(normalizedQuery) ? 1 : 2)
                .ThenBy(node => node.DisplayName);

            int resolvedLimit = limit <= 0 ? 20 : limit;

            List<CodeNode> nodes = await nodesQuery
                .Take(resolvedLimit)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            List<long> nodeIds = nodes.Select(node => node.Id).ToList();
            Dictionary<long, NodeLocation> locations = await GetNodeLocationLookupAsync(nodeIds, LocationType.Definition, ct).ConfigureAwait(false);

            List<SymbolSearchResult> results = new List<SymbolSearchResult>();
            foreach (CodeNode node in nodes)
            {
                NodeLocation? location;
                locations.TryGetValue(node.Id, out location);

                SymbolSearchResult result = new SymbolSearchResult
                {
                    Id = node.Id,
                    DisplayName = node.DisplayName ?? node.SerializedName,
                    SerializedName = node.SerializedName,
                    Kind = node.Type.ToString(),
                    FilePath = location == null ? null : location.FilePath,
                    Line = location == null ? null : location.Line
                };

                results.Add(result);
            }

            return results;
        }

        private static bool IsNodeAllowed(CodeNode node, GraphQueryOptions options)
        {
            if (!options.IncludeMembers && node.ParentNodeId.HasValue)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.NamespaceFilter))
            {
                string prefix = options.NamespaceFilter;
                if (!node.SerializedName.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            if (options.SymbolKindFilter != null && options.SymbolKindFilter.Count > 0
                && !options.SymbolKindFilter.Contains(node.Type))
            {
                return false;
            }

            return true;
        }

        private async Task<Dictionary<long, NodeLocation>> GetNodeLocationLookupAsync(List<long> nodeIds, LocationType locationType, CancellationToken ct)
        {
            Dictionary<long, NodeLocation> locations = new Dictionary<long, NodeLocation>();

            if (nodeIds.Count == 0)
            {
                return locations;
            }

            IQueryable<NodeLocation> query = dbContext.Occurrences
                .AsNoTracking()
                .Where(occurrence => nodeIds.Contains(occurrence.ElementId))
                .Join(dbContext.SourceLocations.AsNoTracking(),
                    occurrence => occurrence.SourceLocationId,
                    location => location.Id,
                    (occurrence, location) => new OccurrenceLocation
                    {
                        ElementId = occurrence.ElementId,
                        FileId = location.FileId,
                        StartLine = location.StartLine,
                        StartColumn = location.StartColumn,
                        EndLine = location.EndLine,
                        EndColumn = location.EndColumn,
                        Type = location.Type
                    })
                .Where(location => location.Type == locationType)
                .Join(dbContext.SourceFiles.AsNoTracking(),
                    location => location.FileId,
                    file => file.Id,
                    (location, file) => new NodeLocation
                    {
                        ElementId = location.ElementId,
                        FilePath = file.Path,
                        Line = location.StartLine,
                        Column = location.StartColumn,
                        EndLine = location.EndLine,
                        EndColumn = location.EndColumn
                    });

            List<NodeLocation> resolvedLocations = await query.ToListAsync(ct).ConfigureAwait(false);

            foreach (NodeLocation location in resolvedLocations)
            {
                if (!locations.ContainsKey(location.ElementId))
                {
                    locations.Add(location.ElementId, location);
                }
            }

            return locations;
        }

        private static string BuildNodeCacheKey(long snapshotId, string serializedName)
        {
            return string.Concat(snapshotId.ToString(), ":", serializedName);
        }

        private static string BuildFileCacheKey(long snapshotId, string relativePath)
        {
            return string.Concat(snapshotId.ToString(), ":", relativePath);
        }

        private void ClearCaches()
        {
            nodeIdCache.Clear();
            fileIdCache.Clear();
        }

        private sealed class NodeTraversal
        {
            public NodeTraversal(long nodeId, int depth)
            {
                NodeId = nodeId;
                Depth = depth;
            }

            public long NodeId { get; }

            public int Depth { get; }
        }

        private sealed class OccurrenceLocation
        {
            public long ElementId { get; set; }

            public long FileId { get; set; }

            public int StartLine { get; set; }

            public int StartColumn { get; set; }

            public int EndLine { get; set; }

            public int EndColumn { get; set; }

            public LocationType Type { get; set; }
        }

        private sealed class NodeLocation
        {
            public long ElementId { get; set; }

            public string FilePath { get; set; } = string.Empty;

            public int Line { get; set; }

            public int Column { get; set; }

            public int EndLine { get; set; }

            public int EndColumn { get; set; }
        }
    }
}
