using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyApp.Application.Abstractions;
using MyApp.Application.CodeAnalysis.DTOs;
using MyApp.CodeAnalysis.Abstractions;
using MyApp.Domain.CodeAnalysis;
using MyApp.Domain.Repositories;
using MyApp.Models.CodeAnalysis;
using Swashbuckle.AspNetCore.Annotations;

namespace MyApp.Controllers.Api
{
    [ApiController]
    [Route("api/code-analysis")]
    [Produces("application/json")]
    public sealed class CodeAnalysisApiController : ControllerBase
    {
        private readonly IIndexingJobService indexingJobService;
        private readonly ICodeGraphRepository codeGraphRepository;
        private readonly ILocalRepositoryService localRepositoryService;
        private readonly ILogger<CodeAnalysisApiController> logger;

        public CodeAnalysisApiController(IIndexingJobService indexingJobService, ICodeGraphRepository codeGraphRepository, ILocalRepositoryService localRepositoryService, ILogger<CodeAnalysisApiController> logger)
        {
            this.indexingJobService = indexingJobService;
            this.codeGraphRepository = codeGraphRepository;
            this.localRepositoryService = localRepositoryService;
            this.logger = logger;
        }

        /// <summary>
        /// Queues a repository for background indexing.
        /// </summary>
        /// <param name="request">The repository indexing request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The queued indexing job response.</returns>
        [HttpPost("index")]
        [SwaggerOperation(Summary = "Queue a repository for indexing", Description = "Queues a repository for background indexing using its repositoryId.", OperationId = "QueueCodeAnalysisIndex")]
        [ProducesResponseType(typeof(IndexingStartResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> IndexRepository([FromBody] CodeAnalysisIndexRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            LocalRepository? repository = FindRepository(request.RepositoryId);
            if (repository == null)
            {
                return NotFound(CreateProblemDetails(StatusCodes.Status404NotFound, "Repository not found", "The requested repositoryId does not exist."));
            }

            try
            {
                long snapshotId = await indexingJobService.QueueIndexingAsync(request.RepositoryId, cancellationToken).ConfigureAwait(false);
                IndexingStartResponse response = new IndexingStartResponse
                {
                    JobId = snapshotId,
                    Status = IndexingStatus.Queued.ToString(),
                    Message = "Indexing started."
                };

                return Ok(response);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to queue indexing for repository {RepositoryId}", request.RepositoryId);
                return StatusCode(StatusCodes.Status500InternalServerError, CreateProblemDetails(StatusCodes.Status500InternalServerError, "Indexing failed", "An unexpected error occurred while starting indexing."));
            }
        }

        /// <summary>
        /// Gets the current indexing status for a repository.
        /// </summary>
        /// <param name="repositoryId">Repository identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The indexing status.</returns>
        [HttpGet("status")]
        [SwaggerOperation(Summary = "Get indexing status", Description = "Returns the latest indexing status for a repository.", OperationId = "GetCodeAnalysisStatus")]
        [ProducesResponseType(typeof(IndexingJobStatus), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetStatus([FromQuery(Name = "repositoryId")] string repositoryId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(repositoryId))
            {
                return BadRequest(CreateProblemDetails(StatusCodes.Status400BadRequest, "Repository id required", "The repositoryId query parameter is required."));
            }

            LocalRepository? repository = FindRepository(repositoryId);
            if (repository == null)
            {
                return NotFound(CreateProblemDetails(StatusCodes.Status404NotFound, "Repository not found", "The requested repositoryId does not exist."));
            }

            IndexingJobStatus? status = await indexingJobService.GetJobStatusAsync(repositoryId, cancellationToken).ConfigureAwait(false);
            if (status == null)
            {
                return NotFound(CreateProblemDetails(StatusCodes.Status404NotFound, "Indexing status not found", "No indexing status was found for the repository."));
            }

            return Ok(status);
        }

        /// <summary>
        /// Gets graph data for the repository.
        /// </summary>
        /// <param name="repositoryId">Repository identifier.</param>
        /// <param name="maxDepth">Maximum traversal depth.</param>
        /// <param name="maxNodes">Maximum nodes to return.</param>
        /// <param name="maxEdges">Maximum edges to return.</param>
        /// <param name="includeMembers">Whether to include member nodes.</param>
        /// <param name="rootNodeId">Optional root node identifier.</param>
        /// <param name="namespaceFilter">Namespace prefix filter.</param>
        /// <param name="symbolKinds">Comma-separated symbol kind filter.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Graph nodes and edges.</returns>
        [HttpGet("graph")]
        [SwaggerOperation(Summary = "Get graph data", Description = "Returns graph nodes and edges for a repository.", OperationId = "GetCodeAnalysisGraph")]
        [ProducesResponseType(typeof(GraphData), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetGraph([FromQuery(Name = "repositoryId")] string repositoryId, [FromQuery(Name = "maxDepth")] int maxDepth, [FromQuery(Name = "maxNodes")] int maxNodes, [FromQuery(Name = "maxEdges")] int maxEdges, [FromQuery(Name = "includeMembers")] bool includeMembers, [FromQuery(Name = "rootNodeId")] long? rootNodeId, [FromQuery(Name = "namespaceFilter")] string? namespaceFilter, [FromQuery(Name = "symbolKinds")] string? symbolKinds, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(repositoryId))
            {
                return BadRequest(CreateProblemDetails(StatusCodes.Status400BadRequest, "Repository id required", "The repositoryId query parameter is required."));
            }

            LocalRepository? repository = FindRepository(repositoryId);
            if (repository == null)
            {
                return NotFound(CreateProblemDetails(StatusCodes.Status404NotFound, "Repository not found", "The requested repositoryId does not exist."));
            }

            List<CSharpSymbolKind>? symbolKindFilter = null;
            if (!string.IsNullOrWhiteSpace(symbolKinds))
            {
                List<string> kindTokens = symbolKinds.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                symbolKindFilter = new List<CSharpSymbolKind>();

                foreach (string token in kindTokens)
                {
                    string trimmedToken = token.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedToken))
                    {
                        continue;
                    }

                    CSharpSymbolKind kind;
                    if (!Enum.TryParse(trimmedToken, true, out kind))
                    {
                        return BadRequest(CreateProblemDetails(StatusCodes.Status400BadRequest, "Invalid symbolKinds", $"Unsupported symbol kind: {trimmedToken}."));
                    }

                    symbolKindFilter.Add(kind);
                }
            }

            GraphQueryOptions options = new GraphQueryOptions
            {
                RepositoryId = repositoryId,
                MaxDepth = maxDepth,
                MaxNodes = maxNodes,
                MaxEdges = maxEdges,
                IncludeMembers = includeMembers,
                RootNodeId = rootNodeId,
                NamespaceFilter = namespaceFilter,
                SymbolKindFilter = symbolKindFilter
            };

            GraphData graphData = await codeGraphRepository.GetGraphDataAsync(options, cancellationToken).ConfigureAwait(false);
            return Ok(graphData);
        }

        /// <summary>
        /// Gets reference locations for a symbol.
        /// </summary>
        /// <param name="symbolId">Symbol identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Reference locations for the symbol.</returns>
        [HttpGet("symbols/{symbolId:long}/references")]
        [SwaggerOperation(Summary = "Get symbol references", Description = "Returns source locations where a symbol is referenced.", OperationId = "GetCodeAnalysisSymbolReferences")]
        [ProducesResponseType(typeof(List<ReferenceLocation>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetSymbolReferences([FromRoute] long symbolId, CancellationToken cancellationToken)
        {
            bool symbolExists = await codeGraphRepository.SymbolExistsAsync(symbolId, cancellationToken).ConfigureAwait(false);
            if (!symbolExists)
            {
                return NotFound(CreateProblemDetails(StatusCodes.Status404NotFound, "Symbol not found", "The requested symbol was not found."));
            }

            List<ReferenceLocation> references = await codeGraphRepository.GetSymbolReferencesAsync(symbolId, cancellationToken).ConfigureAwait(false);
            return Ok(references);
        }

        /// <summary>
        /// Gets callers for a symbol.
        /// </summary>
        /// <param name="symbolId">Symbol identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Callers for the symbol.</returns>
        [HttpGet("symbols/{symbolId:long}/callers")]
        [SwaggerOperation(Summary = "Get callers", Description = "Returns methods that call a symbol.", OperationId = "GetCodeAnalysisCallers")]
        [ProducesResponseType(typeof(List<SymbolSearchResult>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetCallers([FromRoute] long symbolId, CancellationToken cancellationToken)
        {
            bool symbolExists = await codeGraphRepository.SymbolExistsAsync(symbolId, cancellationToken).ConfigureAwait(false);
            if (!symbolExists)
            {
                return NotFound(CreateProblemDetails(StatusCodes.Status404NotFound, "Symbol not found", "The requested symbol was not found."));
            }

            List<SymbolSearchResult> callers = await codeGraphRepository.GetCallersAsync(symbolId, cancellationToken).ConfigureAwait(false);
            return Ok(callers);
        }

        /// <summary>
        /// Gets callees for a symbol.
        /// </summary>
        /// <param name="symbolId">Symbol identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Callees for the symbol.</returns>
        [HttpGet("symbols/{symbolId:long}/callees")]
        [SwaggerOperation(Summary = "Get callees", Description = "Returns methods called by a symbol.", OperationId = "GetCodeAnalysisCallees")]
        [ProducesResponseType(typeof(List<SymbolSearchResult>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetCallees([FromRoute] long symbolId, CancellationToken cancellationToken)
        {
            bool symbolExists = await codeGraphRepository.SymbolExistsAsync(symbolId, cancellationToken).ConfigureAwait(false);
            if (!symbolExists)
            {
                return NotFound(CreateProblemDetails(StatusCodes.Status404NotFound, "Symbol not found", "The requested symbol was not found."));
            }

            List<SymbolSearchResult> callees = await codeGraphRepository.GetCalleesAsync(symbolId, cancellationToken).ConfigureAwait(false);
            return Ok(callees);
        }

        /// <summary>
        /// Gets inheritance relationships for a symbol.
        /// </summary>
        /// <param name="symbolId">Symbol identifier.</param>
        /// <param name="ancestors">Include ancestor types.</param>
        /// <param name="descendants">Include descendant types.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Inheritance relationships.</returns>
        [HttpGet("inheritance/{symbolId:long}")]
        [SwaggerOperation(Summary = "Get inheritance tree", Description = "Returns ancestor and descendant types for a symbol.", OperationId = "GetCodeAnalysisInheritance")]
        [ProducesResponseType(typeof(InheritanceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetInheritance([FromRoute] long symbolId, [FromQuery(Name = "ancestors")] bool? ancestors, [FromQuery(Name = "descendants")] bool? descendants, CancellationToken cancellationToken)
        {
            bool symbolExists = await codeGraphRepository.SymbolExistsAsync(symbolId, cancellationToken).ConfigureAwait(false);
            if (!symbolExists)
            {
                return NotFound(CreateProblemDetails(StatusCodes.Status404NotFound, "Symbol not found", "The requested symbol was not found."));
            }

            bool includeAncestors = ancestors ?? true;
            bool includeDescendants = descendants ?? true;

            InheritanceResponse inheritance = await codeGraphRepository.GetInheritanceAsync(symbolId, includeAncestors, includeDescendants, cancellationToken).ConfigureAwait(false);
            return Ok(inheritance);
        }

        /// <summary>
        /// Searches symbols within a repository.
        /// </summary>
        /// <param name="repositoryId">Repository identifier.</param>
        /// <param name="query">Search query.</param>
        /// <param name="limit">Maximum number of results.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Symbol search results.</returns>
        [HttpGet("search")]
        [SwaggerOperation(Summary = "Search symbols", Description = "Searches symbols by name within a repository.", OperationId = "SearchCodeAnalysisSymbols")]
        [ProducesResponseType(typeof(List<SymbolSearchResult>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SearchSymbols([FromQuery(Name = "repositoryId")] string repositoryId, [FromQuery(Name = "query")] string query, [FromQuery(Name = "limit")] int limit, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(repositoryId))
            {
                return BadRequest(CreateProblemDetails(StatusCodes.Status400BadRequest, "Repository id required", "The repositoryId query parameter is required."));
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(CreateProblemDetails(StatusCodes.Status400BadRequest, "Query required", "The query parameter is required."));
            }

            LocalRepository? repository = FindRepository(repositoryId);
            if (repository == null)
            {
                return NotFound(CreateProblemDetails(StatusCodes.Status404NotFound, "Repository not found", "The requested repositoryId does not exist."));
            }

            List<SymbolSearchResult> results = await codeGraphRepository.SearchSymbolsAsync(repositoryId, query, limit, cancellationToken).ConfigureAwait(false);
            return Ok(results);
        }

        /// <summary>
        /// Gets source content for a file.
        /// </summary>
        /// <param name="repositoryId">Repository identifier.</param>
        /// <param name="filePath">Repository-relative file path.</param>
        /// <param name="startLine">Starting line number.</param>
        /// <param name="endLine">Ending line number.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Source content response.</returns>
        [HttpGet("source")]
        [SwaggerOperation(Summary = "Get source content", Description = "Returns source code for a file within the repository.", OperationId = "GetCodeAnalysisSource")]
        [ProducesResponseType(typeof(SourceContentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetSource([FromQuery(Name = "repositoryId")] string repositoryId, [FromQuery(Name = "filePath")] string filePath, [FromQuery(Name = "startLine")] int? startLine, [FromQuery(Name = "endLine")] int? endLine, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(repositoryId))
            {
                return BadRequest(CreateProblemDetails(StatusCodes.Status400BadRequest, "Repository id required", "The repositoryId query parameter is required."));
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return BadRequest(CreateProblemDetails(StatusCodes.Status400BadRequest, "File path required", "The filePath query parameter is required."));
            }

            LocalRepository? repository = FindRepository(repositoryId);
            if (repository == null)
            {
                return NotFound(CreateProblemDetails(StatusCodes.Status404NotFound, "Repository not found", "The requested repositoryId does not exist."));
            }

            string resolvedPath;
            if (!TryResolveRepositoryFilePath(repository.FullPath, filePath, out resolvedPath))
            {
                return BadRequest(CreateProblemDetails(StatusCodes.Status400BadRequest, "Invalid file path", "The filePath must be within the repository root."));
            }

            if (!System.IO.File.Exists(resolvedPath))
            {
                return NotFound(CreateProblemDetails(StatusCodes.Status404NotFound, "File not found", "The requested file does not exist."));
            }

            List<string> lines = (await System.IO.File.ReadAllLinesAsync(resolvedPath, cancellationToken).ConfigureAwait(false)).ToList();

            int start = startLine.HasValue && startLine.Value > 0 ? startLine.Value : 1;
            int end = endLine.HasValue && endLine.Value > 0 ? endLine.Value : lines.Count;

            if (start > end)
            {
                return BadRequest(CreateProblemDetails(StatusCodes.Status400BadRequest, "Invalid line range", "The startLine must be less than or equal to endLine."));
            }

            if (lines.Count == 0)
            {
                return Ok(new SourceContentResponse
                {
                    Content = string.Empty,
                    Language = "csharp"
                });
            }

            int safeStart = Math.Max(1, start);
            int safeEnd = Math.Min(lines.Count, end);
            List<string> selectedLines = lines.Skip(safeStart - 1).Take(safeEnd - safeStart + 1).ToList();

            SourceContentResponse response = new SourceContentResponse
            {
                Content = string.Join(Environment.NewLine, selectedLines),
                Language = "csharp"
            };

            return Ok(response);
        }

        private LocalRepository? FindRepository(string repositoryId)
        {
            if (string.IsNullOrWhiteSpace(repositoryId))
            {
                return null;
            }

            IReadOnlyCollection<LocalRepository> repositories = localRepositoryService.GetRepositories();
            return repositories.FirstOrDefault(repository => string.Equals(repository.Name, repositoryId, StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryResolveRepositoryFilePath(string repositoryRoot, string filePath, out string resolvedPath)
        {
            resolvedPath = string.Empty;

            if (string.IsNullOrWhiteSpace(repositoryRoot) || string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            if (Path.IsPathRooted(filePath))
            {
                return false;
            }

            string normalizedRoot = Path.GetFullPath(repositoryRoot);
            if (!normalizedRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                normalizedRoot = normalizedRoot + Path.DirectorySeparatorChar;
            }

            string combinedPath = Path.GetFullPath(Path.Combine(normalizedRoot, filePath));
            if (!combinedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            resolvedPath = combinedPath;
            return true;
        }

        private static ProblemDetails CreateProblemDetails(int statusCode, string title, string detail)
        {
            return new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail
            };
        }
    }
}
