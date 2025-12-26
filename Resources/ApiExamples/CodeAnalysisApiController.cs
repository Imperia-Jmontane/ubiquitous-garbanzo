// ============================================================================
// REFERENCE FILE: API Controller for Code Analysis
// Complete implementation with security validation and error handling.
// ============================================================================

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MyApp.CodeAnalysis.Reference
{
    /// <summary>
    /// API Controller for Code Analysis endpoints.
    ///
    /// SECURITY CONSIDERATIONS:
    /// 1. NEVER accept raw file paths from user input - use repositoryId
    /// 2. Validate repositoryId against known repositories
    /// 3. Validate all file paths are within repository root
    /// 4. Use pagination to prevent large responses
    /// </summary>
    [ApiController]
    [Route("api/code-analysis")]
    public class CodeAnalysisApiController : ControllerBase
    {
        private readonly IIndexingJobService _indexingService;
        private readonly ICodeGraphRepository _repository;
        private readonly IRepositoryStorageService _repositoryStorage;
        private readonly ILogger<CodeAnalysisApiController> _logger;

        public CodeAnalysisApiController(
            IIndexingJobService indexingService,
            ICodeGraphRepository repository,
            IRepositoryStorageService repositoryStorage,
            ILogger<CodeAnalysisApiController> logger)
        {
            _indexingService = indexingService;
            _repository = repository;
            _repositoryStorage = repositoryStorage;
            _logger = logger;
        }

        // =====================================================================
        // POST /api/code-analysis/index
        // Queues a repository for indexing (returns immediately)
        // =====================================================================
        [HttpPost("index")]
        [ProducesResponseType(typeof(IndexingStartedResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        [ProducesResponseType(typeof(ErrorResponse), 404)]
        public async Task<IActionResult> StartIndexing(
            [FromBody] StartIndexingRequest request,
            CancellationToken ct)
        {
            // SECURITY: Validate repositoryId exists in our system
            LocalRepository? localRepository = await _repositoryStorage.GetRepositoryAsync(request.RepositoryId, ct);

            if (localRepository == null)
            {
                _logger.LogWarning("Index requested for unknown repository: {RepositoryId}", request.RepositoryId);
                return NotFound(new ErrorResponse
                {
                    Error = "RepositoryNotFound",
                    Message = $"Repository '{request.RepositoryId}' not found"
                });
            }

            // Validate path exists
            if (!Directory.Exists(localRepository.FullPath))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "RepositoryPathNotFound",
                    Message = "Repository path does not exist on disk"
                });
            }

            // Queue indexing job
            long snapshotId = await _indexingService.QueueIndexingAsync(
                request.RepositoryId,
                localRepository.FullPath,
                ct);

            _logger.LogInformation(
                "Queued indexing for repository {RepositoryId} (snapshot: {SnapshotId})",
                request.RepositoryId, snapshotId);

            return Ok(new IndexingStartedResponse
            {
                SnapshotId = snapshotId,
                Status = "Queued",
                Message = "Indexing has been queued"
            });
        }

        // =====================================================================
        // GET /api/code-analysis/status
        // Gets the current indexing status for a repository
        // =====================================================================
        [HttpGet("status")]
        [ProducesResponseType(typeof(IndexingJobStatus), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 404)]
        public IActionResult GetStatus([FromQuery] string repositoryId)
        {
            if (string.IsNullOrEmpty(repositoryId))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "InvalidRequest",
                    Message = "repositoryId is required"
                });
            }

            IndexingJobStatus? status = _indexingService.GetJobStatus(repositoryId);

            if (status == null)
            {
                return NotFound(new ErrorResponse
                {
                    Error = "JobNotFound",
                    Message = $"No indexing job found for repository '{repositoryId}'"
                });
            }

            return Ok(status);
        }

        // =====================================================================
        // GET /api/code-analysis/graph
        // Gets graph data for visualization (with pagination)
        // =====================================================================
        [HttpGet("graph")]
        [ProducesResponseType(typeof(GraphDataResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        public async Task<IActionResult> GetGraph(
            [FromQuery] string repositoryId,
            [FromQuery] int maxDepth = 2,
            [FromQuery] int maxNodes = 100,
            [FromQuery] int maxEdges = 500,
            [FromQuery] bool includeMembers = false,
            [FromQuery] long? rootNodeId = null,
            [FromQuery] string? namespaceFilter = null,
            [FromQuery] string? symbolKinds = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(repositoryId))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "InvalidRequest",
                    Message = "repositoryId is required"
                });
            }

            // SECURITY: Validate repository exists
            LocalRepository? localRepository = await _repositoryStorage.GetRepositoryAsync(repositoryId, ct);
            if (localRepository == null)
            {
                return NotFound(new ErrorResponse { Error = "RepositoryNotFound", Message = "Repository not found" });
            }

            // Parse symbol kinds filter
            List<CSharpSymbolKind>? kindFilter = null;
            if (!string.IsNullOrEmpty(symbolKinds))
            {
                kindFilter = symbolKinds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(k => Enum.TryParse<CSharpSymbolKind>(k.Trim(), true, out CSharpSymbolKind result) ? result : (CSharpSymbolKind?)null)
                    .Where(k => k.HasValue)
                    .Select(k => k!.Value)
                    .ToList();
            }

            // Enforce reasonable limits
            maxNodes = Math.Clamp(maxNodes, 1, 1000);
            maxEdges = Math.Clamp(maxEdges, 1, 5000);
            maxDepth = Math.Clamp(maxDepth, 1, 10);

            GraphQueryOptions options = new GraphQueryOptions
            {
                RepositoryId = repositoryId,
                MaxDepth = maxDepth,
                MaxNodes = maxNodes,
                MaxEdges = maxEdges,
                IncludeMembers = includeMembers,
                RootNodeId = rootNodeId,
                NamespaceFilter = namespaceFilter,
                SymbolKindFilter = kindFilter
            };

            GraphData graphData = await _repository.GetGraphDataAsync(options, ct);

            return Ok(new GraphDataResponse
            {
                Nodes = graphData.Nodes.Select(n => new GraphNodeDto
                {
                    Id = n.Id,
                    SerializedName = n.SerializedName,
                    DisplayName = n.DisplayName,
                    Type = n.Type.ToString(),  // String for frontend
                    FilePath = n.FilePath,
                    Line = n.Line,
                    Column = n.Column,
                    ParentId = n.ParentId
                }).ToList(),
                Edges = graphData.Edges.Select(e => new GraphEdgeDto
                {
                    Id = e.Id,
                    SourceNodeId = e.SourceNodeId,
                    TargetNodeId = e.TargetNodeId,
                    Type = e.Type.ToString()  // String for frontend
                }).ToList(),
                HasMore = graphData.HasMore
            });
        }

        // =====================================================================
        // GET /api/code-analysis/symbols/{symbolId}/references
        // Gets all locations where a symbol is referenced
        // =====================================================================
        [HttpGet("symbols/{symbolId:long}/references")]
        [ProducesResponseType(typeof(List<ReferenceLocationDto>), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 404)]
        public async Task<IActionResult> GetSymbolReferences(
            long symbolId,
            CancellationToken ct)
        {
            List<ReferenceLocation> references = await _repository.GetSymbolReferencesAsync(symbolId, ct);

            if (references.Count == 0)
            {
                // Check if symbol exists
                // (In real implementation, would check symbol exists)
            }

            return Ok(references.Select(r => new ReferenceLocationDto
            {
                FilePath = r.FilePath,
                Line = r.Line,
                Column = r.Column,
                EndLine = r.EndLine,
                EndColumn = r.EndColumn,
                Context = r.Context
            }).ToList());
        }

        // =====================================================================
        // GET /api/code-analysis/symbols/{symbolId}/callers
        // Gets all methods that call this method
        // =====================================================================
        [HttpGet("symbols/{symbolId:long}/callers")]
        [ProducesResponseType(typeof(List<CallerDto>), 200)]
        public async Task<IActionResult> GetCallers(long symbolId, CancellationToken ct)
        {
            List<CodeNode> callers = await _repository.GetCallersAsync(symbolId, ct);

            return Ok(callers.Select(c => new CallerDto
            {
                Id = c.Id,
                DisplayName = c.DisplayName,
                Kind = c.Type.ToString(),
                FilePath = c.FilePath,
                Line = c.Line
            }).ToList());
        }

        // =====================================================================
        // GET /api/code-analysis/symbols/{symbolId}/callees
        // Gets all methods that this method calls
        // =====================================================================
        [HttpGet("symbols/{symbolId:long}/callees")]
        [ProducesResponseType(typeof(List<CalleeDto>), 200)]
        public async Task<IActionResult> GetCallees(long symbolId, CancellationToken ct)
        {
            List<CodeNode> callees = await _repository.GetCalleesAsync(symbolId, ct);

            return Ok(callees.Select(c => new CalleeDto
            {
                Id = c.Id,
                DisplayName = c.DisplayName,
                Kind = c.Type.ToString(),
                FilePath = c.FilePath,
                Line = c.Line
            }).ToList());
        }

        // =====================================================================
        // GET /api/code-analysis/inheritance/{symbolId}
        // Gets inheritance hierarchy (ancestors and descendants)
        // =====================================================================
        [HttpGet("inheritance/{symbolId:long}")]
        [ProducesResponseType(typeof(InheritanceResponse), 200)]
        public async Task<IActionResult> GetInheritance(
            long symbolId,
            [FromQuery] bool ancestors = true,
            [FromQuery] bool descendants = true,
            CancellationToken ct = default)
        {
            InheritanceData data = await _repository.GetInheritanceHierarchyAsync(
                symbolId, ancestors, descendants, ct);

            return Ok(new InheritanceResponse
            {
                Ancestors = data.Ancestors.Select(n => new InheritanceNodeDto
                {
                    Id = n.Id,
                    DisplayName = n.DisplayName,
                    Kind = n.Type.ToString()
                }).ToList(),
                Descendants = data.Descendants.Select(n => new InheritanceNodeDto
                {
                    Id = n.Id,
                    DisplayName = n.DisplayName,
                    Kind = n.Type.ToString()
                }).ToList()
            });
        }

        // =====================================================================
        // GET /api/code-analysis/search
        // Searches for symbols by name
        // =====================================================================
        [HttpGet("search")]
        [ProducesResponseType(typeof(List<SymbolSearchResultDto>), 200)]
        public async Task<IActionResult> SearchSymbols(
            [FromQuery] string repositoryId,
            [FromQuery] string query,
            [FromQuery] int limit = 20,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(repositoryId) || string.IsNullOrEmpty(query))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "InvalidRequest",
                    Message = "repositoryId and query are required"
                });
            }

            // Enforce limit
            limit = Math.Clamp(limit, 1, 100);

            List<SymbolSearchResult> results = await _repository.SearchSymbolsAsync(
                repositoryId, query, limit, ct);

            return Ok(results.Select(r => new SymbolSearchResultDto
            {
                Id = r.Id,
                DisplayName = r.DisplayName,
                SerializedName = r.SerializedName,
                Kind = r.Kind.ToString(),
                FilePath = r.FilePath,
                Line = r.Line
            }).ToList());
        }

        // =====================================================================
        // GET /api/code-analysis/source
        // Gets source code content (with security validation)
        // =====================================================================
        [HttpGet("source")]
        [ProducesResponseType(typeof(SourceContentResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        [ProducesResponseType(typeof(ErrorResponse), 404)]
        public async Task<IActionResult> GetSourceContent(
            [FromQuery] string repositoryId,
            [FromQuery] string filePath,
            [FromQuery] int? startLine = null,
            [FromQuery] int? endLine = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(repositoryId) || string.IsNullOrEmpty(filePath))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "InvalidRequest",
                    Message = "repositoryId and filePath are required"
                });
            }

            // SECURITY: Get repository to validate it exists
            LocalRepository? localRepository = await _repositoryStorage.GetRepositoryAsync(repositoryId, ct);
            if (localRepository == null)
            {
                return NotFound(new ErrorResponse { Error = "RepositoryNotFound", Message = "Repository not found" });
            }

            // SECURITY: Validate file path is within repository
            // This is CRITICAL to prevent path traversal attacks
            string fullPath = Path.GetFullPath(Path.Combine(localRepository.FullPath, filePath));
            string repositoryRoot = Path.GetFullPath(localRepository.FullPath);

            if (!fullPath.StartsWith(repositoryRoot, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Path traversal attempt: requested '{FilePath}' in repository '{RepositoryId}'",
                    filePath, repositoryId);

                return BadRequest(new ErrorResponse
                {
                    Error = "InvalidFilePath",
                    Message = "File path is outside repository bounds"
                });
            }

            // Check file exists
            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound(new ErrorResponse
                {
                    Error = "FileNotFound",
                    Message = $"File not found: {filePath}"
                });
            }

            // Read file content
            string[] lines = await System.IO.File.ReadAllLinesAsync(fullPath, ct);

            // Apply line range if specified
            string content;
            if (startLine.HasValue && endLine.HasValue)
            {
                int start = Math.Max(0, startLine.Value - 1);  // Convert to 0-based
                int end = Math.Min(lines.Length, endLine.Value);
                content = string.Join(Environment.NewLine, lines.Skip(start).Take(end - start));
            }
            else
            {
                content = string.Join(Environment.NewLine, lines);
            }

            return Ok(new SourceContentResponse
            {
                Content = content,
                Language = Path.GetExtension(fullPath).ToLowerInvariant() switch
                {
                    ".cs" => "csharp",
                    ".js" => "javascript",
                    ".ts" => "typescript",
                    ".json" => "json",
                    ".xml" => "xml",
                    ".html" => "html",
                    ".css" => "css",
                    _ => "plaintext"
                },
                TotalLines = lines.Length
            });
        }

        // =====================================================================
        // DELETE /api/code-analysis/index/{repositoryId}
        // Cancels a running indexing job
        // =====================================================================
        [HttpDelete("index/{repositoryId}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(typeof(ErrorResponse), 404)]
        public IActionResult CancelIndexing(string repositoryId)
        {
            bool cancelled = _indexingService.CancelJob(repositoryId);

            if (!cancelled)
            {
                return NotFound(new ErrorResponse
                {
                    Error = "JobNotFound",
                    Message = "No active indexing job found for this repository"
                });
            }

            return Ok(new { Message = "Indexing job cancelled" });
        }
    }

    // =========================================================================
    // REQUEST/RESPONSE DTOs
    // =========================================================================

    public class StartIndexingRequest
    {
        public string RepositoryId { get; set; } = string.Empty;
    }

    public class IndexingStartedResponse
    {
        public long SnapshotId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class ErrorResponse
    {
        public string Error { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class GraphDataResponse
    {
        public List<GraphNodeDto> Nodes { get; set; } = new();
        public List<GraphEdgeDto> Edges { get; set; } = new();
        public bool HasMore { get; set; }
    }

    public class GraphNodeDto
    {
        public long Id { get; set; }
        public string SerializedName { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string Type { get; set; } = string.Empty;  // String enum name
        public string? FilePath { get; set; }
        public int? Line { get; set; }
        public int? Column { get; set; }
        public long? ParentId { get; set; }
    }

    public class GraphEdgeDto
    {
        public long Id { get; set; }
        public long SourceNodeId { get; set; }
        public long TargetNodeId { get; set; }
        public string Type { get; set; } = string.Empty;  // String enum name
    }

    public class ReferenceLocationDto
    {
        public string FilePath { get; set; } = string.Empty;
        public int Line { get; set; }
        public int Column { get; set; }
        public int EndLine { get; set; }
        public int EndColumn { get; set; }
        public string? Context { get; set; }
    }

    public class CallerDto
    {
        public long Id { get; set; }
        public string? DisplayName { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string? FilePath { get; set; }
        public int? Line { get; set; }
    }

    public class CalleeDto
    {
        public long Id { get; set; }
        public string? DisplayName { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string? FilePath { get; set; }
        public int? Line { get; set; }
    }

    public class InheritanceResponse
    {
        public List<InheritanceNodeDto> Ancestors { get; set; } = new();
        public List<InheritanceNodeDto> Descendants { get; set; } = new();
    }

    public class InheritanceNodeDto
    {
        public long Id { get; set; }
        public string? DisplayName { get; set; }
        public string Kind { get; set; } = string.Empty;
    }

    public class SymbolSearchResultDto
    {
        public long Id { get; set; }
        public string? DisplayName { get; set; }
        public string SerializedName { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string? FilePath { get; set; }
        public int? Line { get; set; }
    }

    public class SourceContentResponse
    {
        public string Content { get; set; } = string.Empty;
        public string Language { get; set; } = "plaintext";
        public int TotalLines { get; set; }
    }

    // =========================================================================
    // PLACEHOLDER INTERFACES (would be in Domain/Services)
    // =========================================================================

    public interface IIndexingJobService
    {
        Task<long> QueueIndexingAsync(string repositoryId, string repositoryPath, CancellationToken ct);
        IndexingJobStatus? GetJobStatus(string repositoryId);
        bool CancelJob(string repositoryId);
    }

    public interface ICodeGraphRepository
    {
        Task<GraphData> GetGraphDataAsync(GraphQueryOptions options, CancellationToken ct);
        Task<List<ReferenceLocation>> GetSymbolReferencesAsync(long symbolId, CancellationToken ct);
        Task<List<SymbolSearchResult>> SearchSymbolsAsync(string repositoryId, string query, int limit, CancellationToken ct);
        Task<List<CodeNode>> GetCallersAsync(long symbolId, CancellationToken ct);
        Task<List<CodeNode>> GetCalleesAsync(long symbolId, CancellationToken ct);
        Task<InheritanceData> GetInheritanceHierarchyAsync(long symbolId, bool ancestors, bool descendants, CancellationToken ct);
    }

    public interface IRepositoryStorageService
    {
        Task<LocalRepository?> GetRepositoryAsync(string repositoryId, CancellationToken ct);
    }

    // Placeholder domain classes
    public class LocalRepository { public string FullPath { get; set; } = string.Empty; }
    public class IndexingJobStatus { public string RepositoryId { get; set; } = string.Empty; }
    public class GraphQueryOptions
    {
        public string RepositoryId { get; set; } = string.Empty;
        public int MaxDepth { get; set; }
        public int MaxNodes { get; set; }
        public int MaxEdges { get; set; }
        public bool IncludeMembers { get; set; }
        public long? RootNodeId { get; set; }
        public string? NamespaceFilter { get; set; }
        public List<CSharpSymbolKind>? SymbolKindFilter { get; set; }
    }
    public class GraphData
    {
        public List<GraphNode> Nodes { get; set; } = new();
        public List<GraphEdge> Edges { get; set; } = new();
        public bool HasMore { get; set; }
    }
    public class GraphNode
    {
        public long Id { get; set; }
        public string SerializedName { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public CSharpSymbolKind Type { get; set; }
        public string? FilePath { get; set; }
        public int? Line { get; set; }
        public int? Column { get; set; }
        public long? ParentId { get; set; }
    }
    public class GraphEdge
    {
        public long Id { get; set; }
        public long SourceNodeId { get; set; }
        public long TargetNodeId { get; set; }
        public CSharpReferenceKind Type { get; set; }
    }
    public class ReferenceLocation
    {
        public string FilePath { get; set; } = string.Empty;
        public int Line { get; set; }
        public int Column { get; set; }
        public int EndLine { get; set; }
        public int EndColumn { get; set; }
        public string? Context { get; set; }
    }
    public class SymbolSearchResult
    {
        public long Id { get; set; }
        public string? DisplayName { get; set; }
        public string SerializedName { get; set; } = string.Empty;
        public CSharpSymbolKind Kind { get; set; }
        public string? FilePath { get; set; }
        public int? Line { get; set; }
    }
    public class CodeNode
    {
        public long Id { get; set; }
        public string? DisplayName { get; set; }
        public CSharpSymbolKind Type { get; set; }
        public string? FilePath { get; set; }
        public int? Line { get; set; }
    }
    public class InheritanceData
    {
        public List<CodeNode> Ancestors { get; set; } = new();
        public List<CodeNode> Descendants { get; set; } = new();
    }
    public enum CSharpSymbolKind { Unknown, Namespace, Class, Struct, Interface, Enum, Method }
    public enum CSharpReferenceKind { Unknown, Inheritance, InterfaceImplementation, Call, TypeUsage, Contains }
}
