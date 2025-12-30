using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyApp.Application.Abstractions;
using MyApp.Application.CodeAnalysis.DTOs;
using MyApp.CodeAnalysis.Indexing;
using MyApp.Domain.CodeAnalysis;
using MyApp.Domain.Repositories;

namespace MyApp.Infrastructure.CodeAnalysis
{
    public sealed class IndexingJobService : IIndexingJobService
    {
        private readonly ConcurrentDictionary<string, IndexingJobState> jobStates;
        private readonly IServiceScopeFactory scopeFactory;
        private readonly ILocalRepositoryService localRepositoryService;
        private readonly ILogger<IndexingJobService> logger;

        public IndexingJobService(IServiceScopeFactory scopeFactory, ILocalRepositoryService localRepositoryService, ILogger<IndexingJobService> logger)
        {
            this.scopeFactory = scopeFactory;
            this.localRepositoryService = localRepositoryService;
            this.logger = logger;
            jobStates = new ConcurrentDictionary<string, IndexingJobState>(StringComparer.OrdinalIgnoreCase);
        }

        public async Task<long> QueueIndexingAsync(string repositoryId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(repositoryId))
            {
                throw new ArgumentException("Repository id must be provided.", nameof(repositoryId));
            }

            if (jobStates.TryGetValue(repositoryId, out IndexingJobState? existingState) && existingState != null)
            {
                return existingState.SnapshotId;
            }

            LocalRepository? repository = FindRepository(repositoryId);

            if (repository == null)
            {
                throw new InvalidOperationException($"Repository not found: {repositoryId}");
            }

            long snapshotId;

            using (IServiceScope scope = scopeFactory.CreateScope())
            {
                ICodeGraphRepository codeGraphRepository = scope.ServiceProvider.GetRequiredService<ICodeGraphRepository>();
                snapshotId = await codeGraphRepository.CreateRepositorySnapshotAsync(repositoryId, repository.FullPath, null, null, ct).ConfigureAwait(false);
            }

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            IndexingJobState jobState = new IndexingJobState(snapshotId, cancellationTokenSource)
            {
                RepositoryId = repositoryId,
                Status = IndexingStatus.Queued
            };

            if (!jobStates.TryAdd(repositoryId, jobState))
            {
                cancellationTokenSource.Dispose();
                return snapshotId;
            }

            jobState.RunningTask = Task.Run(() => RunIndexingJobAsync(repositoryId, repository, jobState, cancellationTokenSource.Token), CancellationToken.None);

            return snapshotId;
        }

        public async Task<IndexingJobStatus?> GetJobStatusAsync(string repositoryId, CancellationToken ct)
        {
            if (jobStates.TryGetValue(repositoryId, out IndexingJobState? jobState) && jobState != null)
            {
                return jobState.ToStatus();
            }

            using (IServiceScope scope = scopeFactory.CreateScope())
            {
                ICodeGraphRepository codeGraphRepository = scope.ServiceProvider.GetRequiredService<ICodeGraphRepository>();
                IndexedRepository? snapshot = await codeGraphRepository.GetRepositorySnapshotAsync(repositoryId, ct).ConfigureAwait(false);

                if (snapshot == null)
                {
                    return null;
                }

                int progress = snapshot.Status == IndexingStatus.Completed ? 100 : 0;

                return new IndexingJobStatus
                {
                    RepositoryId = snapshot.RepositoryId,
                    Status = snapshot.Status,
                    StartedAtUtc = snapshot.IndexedAtUtc,
                    CompletedAtUtc = snapshot.Status == IndexingStatus.Completed ? snapshot.IndexedAtUtc : null,
                    FilesIndexed = snapshot.FilesIndexed,
                    TotalFiles = null,
                    CurrentFile = null,
                    ErrorMessage = snapshot.ErrorMessage,
                    ProgressPercent = progress
                };
            }
        }

        public async Task<bool> CancelJobAsync(string repositoryId, CancellationToken ct)
        {
            if (!jobStates.TryGetValue(repositoryId, out IndexingJobState? jobState) || jobState == null)
            {
                return false;
            }

            jobState.CancellationTokenSource.Cancel();
            jobState.Status = IndexingStatus.Cancelled;
            jobState.CompletedAtUtc = DateTime.UtcNow;

            using (IServiceScope scope = scopeFactory.CreateScope())
            {
                ICodeGraphRepository codeGraphRepository = scope.ServiceProvider.GetRequiredService<ICodeGraphRepository>();
                await codeGraphRepository.UpdateRepositoryStatusAsync(jobState.SnapshotId, IndexingStatus.Cancelled, "Indexing cancelled.", jobState.FilesIndexed ?? 0, jobState.SymbolsCollected ?? 0, jobState.ReferencesCollected ?? 0, TimeSpan.Zero, ct).ConfigureAwait(false);
            }

            return true;
        }

        private async Task RunIndexingJobAsync(string repositoryId, LocalRepository repository, IndexingJobState jobState, CancellationToken ct)
        {
            DateTime startTimeUtc = DateTime.UtcNow;
            jobState.Status = IndexingStatus.Running;
            jobState.StartedAtUtc = startTimeUtc;

            logger.LogInformation("Starting indexing job for repository {RepositoryId}", repositoryId);

            try
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                ICodeGraphRepository codeGraphRepository = scope.ServiceProvider.GetRequiredService<ICodeGraphRepository>();
                ICodeIndexer codeIndexer = scope.ServiceProvider.GetRequiredService<ICodeIndexer>();

                await codeGraphRepository.UpdateRepositoryStatusAsync(jobState.SnapshotId, IndexingStatus.Running, null, 0, 0, 0, TimeSpan.Zero, ct).ConfigureAwait(false);

                IndexingResult result = await IndexRepositoryAsync(codeIndexer, jobState.SnapshotId, repository.FullPath, ct).ConfigureAwait(false);

                jobState.FilesIndexed = result.FilesIndexed;
                jobState.SymbolsCollected = result.SymbolsCollected;
                jobState.ReferencesCollected = result.ReferencesCollected;
                jobState.CompletedAtUtc = DateTime.UtcNow;
                jobState.Status = result.PartialSuccess ? IndexingStatus.Completed : IndexingStatus.Completed;

                string? errorMessage = result.Errors.Count > 0 ? string.Join(Environment.NewLine, result.Errors) : null;
                await codeGraphRepository.UpdateRepositoryStatusAsync(jobState.SnapshotId, IndexingStatus.Completed, errorMessage, result.FilesIndexed, result.SymbolsCollected, result.ReferencesCollected, result.Duration, ct).ConfigureAwait(false);

                logger.LogInformation("Completed indexing job for repository {RepositoryId} in {Duration}ms", repositoryId, result.Duration.TotalMilliseconds);
            }
            catch (OperationCanceledException)
            {
                jobState.Status = IndexingStatus.Cancelled;
                jobState.CompletedAtUtc = DateTime.UtcNow;

                using IServiceScope scope = scopeFactory.CreateScope();
                ICodeGraphRepository codeGraphRepository = scope.ServiceProvider.GetRequiredService<ICodeGraphRepository>();
                await codeGraphRepository.UpdateRepositoryStatusAsync(jobState.SnapshotId, IndexingStatus.Cancelled, "Indexing cancelled.", jobState.FilesIndexed ?? 0, jobState.SymbolsCollected ?? 0, jobState.ReferencesCollected ?? 0, DateTime.UtcNow - startTimeUtc, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                jobState.Status = IndexingStatus.Failed;
                jobState.ErrorMessage = exception.Message;
                jobState.CompletedAtUtc = DateTime.UtcNow;

                logger.LogError(exception, "Indexing job failed for repository {RepositoryId}", repositoryId);

                using IServiceScope scope = scopeFactory.CreateScope();
                ICodeGraphRepository codeGraphRepository = scope.ServiceProvider.GetRequiredService<ICodeGraphRepository>();
                await codeGraphRepository.UpdateRepositoryStatusAsync(jobState.SnapshotId, IndexingStatus.Failed, exception.Message, jobState.FilesIndexed ?? 0, jobState.SymbolsCollected ?? 0, jobState.ReferencesCollected ?? 0, DateTime.UtcNow - startTimeUtc, CancellationToken.None).ConfigureAwait(false);
            }
        }

        private async Task<IndexingResult> IndexRepositoryAsync(ICodeIndexer codeIndexer, long snapshotId, string repositoryPath, CancellationToken ct)
        {
            IEnumerable<string> solutionFiles = WorkspaceLoader.FindSolutionFiles(repositoryPath).ToList();

            if (solutionFiles.Any())
            {
                string solutionPath = solutionFiles.First();

                await RestorePackagesAsync(solutionPath, ct).ConfigureAwait(false);

                return await codeIndexer.IndexSolutionAsync(snapshotId, solutionPath, ct).ConfigureAwait(false);
            }

            IEnumerable<string> projectFiles = WorkspaceLoader.FindProjectFiles(repositoryPath).ToList();

            if (projectFiles.Any())
            {
                string projectPath = projectFiles.First();

                await RestorePackagesAsync(projectPath, ct).ConfigureAwait(false);

                return await codeIndexer.IndexProjectAsync(snapshotId, projectPath, ct).ConfigureAwait(false);
            }

            IndexingResult result = new IndexingResult
            {
                SnapshotId = snapshotId,
                PartialSuccess = false
            };

            result.Errors.Add("No solution or project files were found in the repository.");
            return result;
        }

        private async Task RestorePackagesAsync(string solutionOrProjectPath, CancellationToken ct)
        {
            logger.LogInformation("Restoring NuGet packages for {Path}", solutionOrProjectPath);

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"restore \"{solutionOrProjectPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(startInfo);

            if (process == null)
            {
                logger.LogWarning("Failed to start dotnet restore process");
                return;
            }

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(ct).ConfigureAwait(false);

            string output = await outputTask.ConfigureAwait(false);
            string error = await errorTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                logger.LogWarning("dotnet restore failed with exit code {ExitCode}. Output: {Output}. Error: {Error}",
                    process.ExitCode, output, error);
            }
            else
            {
                logger.LogInformation("dotnet restore completed successfully");
            }
        }

        private LocalRepository? FindRepository(string repositoryId)
        {
            IReadOnlyCollection<LocalRepository> repositories = localRepositoryService.GetRepositories();

            return repositories.FirstOrDefault(repository => string.Equals(repository.Name, repositoryId, StringComparison.OrdinalIgnoreCase));
        }

        private sealed class IndexingJobState
        {
            public IndexingJobState(long snapshotId, CancellationTokenSource cancellationTokenSource)
            {
                SnapshotId = snapshotId;
                CancellationTokenSource = cancellationTokenSource;
            }

            public string RepositoryId { get; set; } = string.Empty;

            public long SnapshotId { get; }

            public IndexingStatus Status { get; set; }

            public DateTime? StartedAtUtc { get; set; }

            public DateTime? CompletedAtUtc { get; set; }

            public int? FilesIndexed { get; set; }

            public int? TotalFiles { get; set; }

            public string? CurrentFile { get; set; }

            public string? ErrorMessage { get; set; }

            public int? SymbolsCollected { get; set; }

            public int? ReferencesCollected { get; set; }

            public CancellationTokenSource CancellationTokenSource { get; }

            public Task? RunningTask { get; set; }

            public IndexingJobStatus ToStatus()
            {
                int progress = 0;

                if (TotalFiles.HasValue && TotalFiles.Value > 0 && FilesIndexed.HasValue)
                {
                    progress = (int)Math.Round((double)FilesIndexed.Value / TotalFiles.Value * 100d);
                }
                else if (Status == IndexingStatus.Completed)
                {
                    progress = 100;
                }

                return new IndexingJobStatus
                {
                    RepositoryId = RepositoryId,
                    Status = Status,
                    StartedAtUtc = StartedAtUtc,
                    CompletedAtUtc = CompletedAtUtc,
                    FilesIndexed = FilesIndexed,
                    TotalFiles = TotalFiles,
                    CurrentFile = CurrentFile,
                    ErrorMessage = ErrorMessage,
                    ProgressPercent = progress
                };
            }
        }
    }
}




