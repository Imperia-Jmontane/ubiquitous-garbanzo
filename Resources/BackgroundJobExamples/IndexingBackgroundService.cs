// ============================================================================
// REFERENCE FILE: Background Service for Code Indexing
//
// INTEGRATION INSTRUCTIONS:
// Create this service in MyApp/MyApp/Infrastructure/CodeAnalysis/
//
// The Flow project already uses similar patterns:
// - IServiceScopeFactory for creating scoped DbContext in background work
// - CancellationToken support throughout
// - ILogger<T> for structured logging
//
// REGISTRATION IN PROGRAM.CS:
// Add after line ~180 (after other service registrations):
//
//     builder.Services.AddSingleton<IIndexingJobService, IndexingBackgroundService>();
//     builder.Services.AddHostedService(sp =>
//         (IndexingBackgroundService)sp.GetRequiredService<IIndexingJobService>());
//
// This dual registration allows:
// - Dependency injection via IIndexingJobService interface
// - Automatic background service hosting
// ============================================================================

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace MyApp.CodeAnalysis.Reference
{
    /// <summary>
    /// Example BackgroundService that processes code indexing jobs.
    ///
    /// WHY USE BACKGROUNDSERVICE?
    /// 1. Indexing can take minutes - don't block HTTP requests
    /// 2. Users can check status and continue working
    /// 3. Can be cancelled if needed
    /// 4. Survives across requests (runs in background)
    ///
    /// KEY CONCEPTS:
    /// - BackgroundService: Long-running hosted service pattern
    /// - Channel<T>: Producer-consumer queue for job requests
    /// - CancellationToken: Graceful shutdown support
    /// - IServiceScopeFactory: Create scoped services in background
    /// </summary>
    public class IndexingBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<IndexingBackgroundService> _logger;
        private readonly CodeAnalysisOptions _options;

        // Channel for job queue (producer-consumer pattern)
        private readonly Channel<IndexingJobRequest> _jobChannel;

        // Track active job state (for status queries)
        private readonly ConcurrentDictionary<string, IndexingJobState> _jobStates;

        // Track cancellation tokens for each job
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _jobCancellations;

        public IndexingBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<IndexingBackgroundService> logger,
            IOptions<CodeAnalysisOptions> options)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _options = options.Value;

            // Bounded channel to prevent memory issues with too many queued jobs
            _jobChannel = Channel.CreateBounded<IndexingJobRequest>(
                new BoundedChannelOptions(100)
                {
                    FullMode = BoundedChannelFullMode.Wait
                });

            _jobStates = new ConcurrentDictionary<string, IndexingJobState>();
            _jobCancellations = new ConcurrentDictionary<string, CancellationTokenSource>();
        }

        // =====================================================================
        // PUBLIC METHODS (called from API controllers)
        // =====================================================================

        /// <summary>
        /// Queues a repository for indexing.
        /// Returns immediately - use GetJobStatus to check progress.
        /// </summary>
        public async Task<long> QueueIndexingAsync(
            string repositoryId,
            string repositoryPath,
            CancellationToken ct)
        {
            // Check if already queued/running
            if (_jobStates.TryGetValue(repositoryId, out IndexingJobState? existingState))
            {
                if (existingState.Status == IndexingStatus.Queued ||
                    existingState.Status == IndexingStatus.Running)
                {
                    _logger.LogWarning(
                        "Repository {RepositoryId} is already being indexed (status: {Status})",
                        repositoryId, existingState.Status);
                    return existingState.SnapshotId;
                }
            }

            // Create snapshot ID (in real impl, this would be from DB)
            long snapshotId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Initialize job state
            IndexingJobState jobState = new IndexingJobState
            {
                RepositoryId = repositoryId,
                SnapshotId = snapshotId,
                Status = IndexingStatus.Queued,
                QueuedAtUtc = DateTime.UtcNow
            };

            _jobStates[repositoryId] = jobState;

            // Create cancellation token for this job
            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _jobCancellations[repositoryId] = cts;

            // Queue the job
            IndexingJobRequest request = new IndexingJobRequest
            {
                RepositoryId = repositoryId,
                RepositoryPath = repositoryPath,
                SnapshotId = snapshotId
            };

            await _jobChannel.Writer.WriteAsync(request, ct);

            _logger.LogInformation(
                "Queued indexing job for repository {RepositoryId} (snapshot: {SnapshotId})",
                repositoryId, snapshotId);

            return snapshotId;
        }

        /// <summary>
        /// Gets the current status of an indexing job.
        /// </summary>
        public IndexingJobStatus? GetJobStatus(string repositoryId)
        {
            if (!_jobStates.TryGetValue(repositoryId, out IndexingJobState? state))
            {
                return null;
            }

            return new IndexingJobStatus
            {
                RepositoryId = state.RepositoryId,
                SnapshotId = state.SnapshotId,
                Status = state.Status,
                StartedAtUtc = state.StartedAtUtc,
                CompletedAtUtc = state.CompletedAtUtc,
                FilesIndexed = state.FilesIndexed,
                TotalFiles = state.TotalFiles,
                CurrentFile = state.CurrentFile,
                ErrorMessage = state.ErrorMessage,
                ProgressPercent = state.TotalFiles > 0
                    ? (int)(state.FilesIndexed * 100.0 / state.TotalFiles)
                    : 0
            };
        }

        /// <summary>
        /// Cancels a running or queued indexing job.
        /// </summary>
        public bool CancelJob(string repositoryId)
        {
            if (_jobCancellations.TryRemove(repositoryId, out CancellationTokenSource? cts))
            {
                cts.Cancel();
                cts.Dispose();

                if (_jobStates.TryGetValue(repositoryId, out IndexingJobState? state))
                {
                    state.Status = IndexingStatus.Cancelled;
                    state.CompletedAtUtc = DateTime.UtcNow;
                }

                _logger.LogInformation("Cancelled indexing job for repository {RepositoryId}", repositoryId);
                return true;
            }

            return false;
        }

        // =====================================================================
        // BACKGROUND SERVICE IMPLEMENTATION
        // =====================================================================

        /// <summary>
        /// Main processing loop - runs continuously in background.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Indexing background service started");

            try
            {
                // Process jobs from the channel until shutdown
                await foreach (IndexingJobRequest job in _jobChannel.Reader.ReadAllAsync(stoppingToken))
                {
                    try
                    {
                        await ProcessJobAsync(job, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        // Application is shutting down
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Unhandled error processing indexing job for {RepositoryId}",
                            job.RepositoryId);
                    }
                }
            }
            finally
            {
                _logger.LogInformation("Indexing background service stopped");
            }
        }

        /// <summary>
        /// Processes a single indexing job.
        /// </summary>
        private async Task ProcessJobAsync(IndexingJobRequest job, CancellationToken stoppingToken)
        {
            string repositoryId = job.RepositoryId;

            // Get or create job-specific cancellation token
            CancellationToken jobCt = stoppingToken;
            if (_jobCancellations.TryGetValue(repositoryId, out CancellationTokenSource? jobCts))
            {
                jobCt = CancellationTokenSource.CreateLinkedTokenSource(
                    stoppingToken, jobCts.Token).Token;
            }

            // Update state to Running
            if (_jobStates.TryGetValue(repositoryId, out IndexingJobState? state))
            {
                state.Status = IndexingStatus.Running;
                state.StartedAtUtc = DateTime.UtcNow;
            }

            _logger.LogInformation("Starting indexing for repository {RepositoryId}", repositoryId);

            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Create a scope for scoped services (DbContext, etc.)
                using IServiceScope scope = _scopeFactory.CreateScope();

                // Get services from the scope
                ICodeIndexer indexer = scope.ServiceProvider.GetRequiredService<ICodeIndexer>();
                ICodeGraphRepository repository = scope.ServiceProvider.GetRequiredService<ICodeGraphRepository>();

                // Create progress callback to update state
                IProgress<IndexingProgress> progress = new Progress<IndexingProgress>(p =>
                {
                    if (_jobStates.TryGetValue(repositoryId, out IndexingJobState? s))
                    {
                        s.FilesIndexed = p.FilesProcessed;
                        s.TotalFiles = p.TotalFiles;
                        s.CurrentFile = p.CurrentFile;
                    }
                });

                // Find solution/project files
                string? solutionPath = FindSolutionFile(job.RepositoryPath);

                IndexingResult result;
                if (solutionPath != null)
                {
                    // Index the solution
                    result = await indexer.IndexSolutionAsync(job.SnapshotId, solutionPath, jobCt);
                }
                else
                {
                    // Try individual project files
                    result = await IndexAllProjectsAsync(indexer, job.SnapshotId, job.RepositoryPath, jobCt);
                }

                stopwatch.Stop();

                // Update final state
                if (_jobStates.TryGetValue(repositoryId, out state))
                {
                    state.Status = result.Errors.Count > 0 && !result.PartialSuccess
                        ? IndexingStatus.Failed
                        : IndexingStatus.Completed;
                    state.CompletedAtUtc = DateTime.UtcNow;
                    state.FilesIndexed = result.FilesIndexed;

                    if (result.Errors.Count > 0)
                    {
                        state.ErrorMessage = string.Join("; ", result.Errors.Take(5));
                    }
                }

                _logger.LogInformation(
                    "Completed indexing for repository {RepositoryId}: {Files} files, {Symbols} symbols, {References} references in {Duration}ms",
                    repositoryId,
                    result.FilesIndexed,
                    result.SymbolsCollected,
                    result.ReferencesCollected,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Indexing cancelled for repository {RepositoryId}", repositoryId);

                if (_jobStates.TryGetValue(repositoryId, out state))
                {
                    state.Status = IndexingStatus.Cancelled;
                    state.CompletedAtUtc = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error indexing repository {RepositoryId}", repositoryId);

                if (_jobStates.TryGetValue(repositoryId, out state))
                {
                    state.Status = IndexingStatus.Failed;
                    state.CompletedAtUtc = DateTime.UtcNow;
                    state.ErrorMessage = ex.Message;
                }
            }
            finally
            {
                // Cleanup cancellation token
                if (_jobCancellations.TryRemove(repositoryId, out CancellationTokenSource? cts))
                {
                    cts.Dispose();
                }
            }
        }

        /// <summary>
        /// Finds a .sln file in the repository.
        /// </summary>
        private string? FindSolutionFile(string repositoryPath)
        {
            // Search in root first, then subdirectories
            string[] solutionFiles = System.IO.Directory.GetFiles(
                repositoryPath, "*.sln", System.IO.SearchOption.AllDirectories);

            // Prefer root-level solution
            string? rootSolution = solutionFiles
                .FirstOrDefault(f => System.IO.Path.GetDirectoryName(f) == repositoryPath);

            return rootSolution ?? solutionFiles.FirstOrDefault();
        }

        /// <summary>
        /// Indexes all .csproj files when no solution is found.
        /// </summary>
        private async Task<IndexingResult> IndexAllProjectsAsync(
            ICodeIndexer indexer,
            long snapshotId,
            string repositoryPath,
            CancellationToken ct)
        {
            string[] projectFiles = System.IO.Directory.GetFiles(
                repositoryPath, "*.csproj", System.IO.SearchOption.AllDirectories);

            // Skip common non-source directories
            projectFiles = projectFiles
                .Where(f => !f.Contains($"{System.IO.Path.DirectorySeparatorChar}bin{System.IO.Path.DirectorySeparatorChar}"))
                .Where(f => !f.Contains($"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}"))
                .ToArray();

            IndexingResult combinedResult = new IndexingResult { SnapshotId = snapshotId };

            foreach (string projectPath in projectFiles)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    IndexingResult result = await indexer.IndexProjectAsync(snapshotId, projectPath, ct);
                    combinedResult.FilesIndexed += result.FilesIndexed;
                    combinedResult.SymbolsCollected += result.SymbolsCollected;
                    combinedResult.ReferencesCollected += result.ReferencesCollected;
                    combinedResult.Errors.AddRange(result.Errors);
                }
                catch (Exception ex)
                {
                    combinedResult.Errors.Add($"Error indexing {System.IO.Path.GetFileName(projectPath)}: {ex.Message}");
                }
            }

            combinedResult.PartialSuccess = combinedResult.FilesIndexed > 0;
            return combinedResult;
        }
    }

    // =========================================================================
    // SUPPORTING CLASSES
    // =========================================================================

    /// <summary>
    /// Job request queued to the channel.
    /// </summary>
    public class IndexingJobRequest
    {
        public string RepositoryId { get; set; } = string.Empty;
        public string RepositoryPath { get; set; } = string.Empty;
        public long SnapshotId { get; set; }
    }

    /// <summary>
    /// Internal job state tracking.
    /// </summary>
    public class IndexingJobState
    {
        public string RepositoryId { get; set; } = string.Empty;
        public long SnapshotId { get; set; }
        public IndexingStatus Status { get; set; }
        public DateTime? QueuedAtUtc { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public int FilesIndexed { get; set; }
        public int TotalFiles { get; set; }
        public string? CurrentFile { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Public status returned to API callers.
    /// </summary>
    public class IndexingJobStatus
    {
        public string RepositoryId { get; set; } = string.Empty;
        public long SnapshotId { get; set; }
        public IndexingStatus Status { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public int FilesIndexed { get; set; }
        public int? TotalFiles { get; set; }
        public string? CurrentFile { get; set; }
        public string? ErrorMessage { get; set; }
        public int ProgressPercent { get; set; }
    }

    /// <summary>
    /// Progress callback data.
    /// </summary>
    public class IndexingProgress
    {
        public int FilesProcessed { get; set; }
        public int TotalFiles { get; set; }
        public string? CurrentFile { get; set; }
    }

    // =========================================================================
    // REGISTRATION IN PROGRAM.CS
    // =========================================================================
    /*
    // Add this in your Program.cs:

    // Register the background service
    builder.Services.AddHostedService<IndexingBackgroundService>();

    // Make it injectable for status queries
    builder.Services.AddSingleton(sp =>
        sp.GetServices<IHostedService>()
          .OfType<IndexingBackgroundService>()
          .First());

    // Or use a simpler pattern with an interface:
    builder.Services.AddSingleton<IIndexingJobService, IndexingBackgroundService>();
    builder.Services.AddHostedService(sp => (IndexingBackgroundService)sp.GetRequiredService<IIndexingJobService>());
    */

    // Placeholder interfaces
    public interface ICodeIndexer
    {
        Task<IndexingResult> IndexSolutionAsync(long snapshotId, string solutionPath, CancellationToken ct);
        Task<IndexingResult> IndexProjectAsync(long snapshotId, string projectPath, CancellationToken ct);
    }

    public interface ICodeGraphRepository { }

    public class IndexingResult
    {
        public long SnapshotId { get; set; }
        public int FilesIndexed { get; set; }
        public int SymbolsCollected { get; set; }
        public int ReferencesCollected { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public bool PartialSuccess { get; set; }
    }

    public class CodeAnalysisOptions
    {
        public int MaxIndexingConcurrency { get; set; } = 1;
        public int IndexingTimeoutMinutes { get; set; } = 30;
    }

    public enum IndexingStatus { Queued = 0, Running = 1, Completed = 2, Failed = 3, Cancelled = 4 }
}
