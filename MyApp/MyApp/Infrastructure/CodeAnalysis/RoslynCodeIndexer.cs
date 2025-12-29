using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyApp.Application.CodeAnalysis.DTOs;
using MyApp.Application.Configuration;
using MyApp.CodeAnalysis.Indexing;
using MyApp.Domain.CodeAnalysis;

namespace MyApp.Infrastructure.CodeAnalysis
{
    public sealed class RoslynCodeIndexer : ICodeIndexer
    {
        private readonly ICodeGraphRepository repository;
        private readonly ILogger<RoslynCodeIndexer> logger;
        private readonly CodeAnalysisOptions options;
        private readonly ILoggerFactory loggerFactory;

        public RoslynCodeIndexer(ICodeGraphRepository repository, ILogger<RoslynCodeIndexer> logger, IOptions<CodeAnalysisOptions> options, ILoggerFactory loggerFactory)
        {
            this.repository = repository;
            this.logger = logger;
            this.options = options.Value;
            this.loggerFactory = loggerFactory;
        }

        public async Task<IndexingResult> IndexSolutionAsync(long snapshotId, string solutionPath, CancellationToken ct)
        {
            return await RunWithTimeoutAsync(token => IndexSolutionInternalAsync(snapshotId, solutionPath, token), ct).ConfigureAwait(false);
        }

        public async Task<IndexingResult> IndexProjectAsync(long snapshotId, string projectPath, CancellationToken ct)
        {
            return await RunWithTimeoutAsync(token => IndexProjectInternalAsync(snapshotId, projectPath, token), ct).ConfigureAwait(false);
        }

        public async Task<IndexingResult> IndexFileAsync(long snapshotId, string filePath, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("The file path must be provided.", nameof(filePath));
            }

            string? projectPath = FindContainingProjectFile(filePath);

            if (projectPath == null)
            {
                IndexingResult missingProjectResult = new IndexingResult
                {
                    SnapshotId = snapshotId,
                    PartialSuccess = false
                };
                missingProjectResult.Errors.Add($"Unable to find a project file for {filePath}.");
                return missingProjectResult;
            }

            return await IndexProjectAsync(snapshotId, projectPath, ct).ConfigureAwait(false);
        }

        private async Task<IndexingResult> IndexSolutionInternalAsync(long snapshotId, string solutionPath, CancellationToken ct)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            IndexingResult result = new IndexingResult
            {
                SnapshotId = snapshotId
            };

            logger.LogInformation("Starting solution indexing for snapshot {SnapshotId} at {SolutionPath}", snapshotId, solutionPath);

            string repositoryRoot = DetermineRepositoryRootPath(solutionPath);

            using WorkspaceLoader workspaceLoader = new WorkspaceLoader(loggerFactory.CreateLogger<WorkspaceLoader>());

            try
            {
                Solution solution = await workspaceLoader.LoadSolutionAsync(solutionPath, ct).ConfigureAwait(false);

                await repository.BeginTransactionAsync(ct).ConfigureAwait(false);

                foreach (Project project in solution.Projects)
                {
                    await IndexProjectAsync(project, snapshotId, repositoryRoot, result, ct).ConfigureAwait(false);
                }

                await repository.CommitTransactionAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await repository.RollbackTransactionAsync(CancellationToken.None).ConfigureAwait(false);
                result.Errors.Add("Indexing was cancelled.");
                result.PartialSuccess = true;
            }
            catch (Exception exception)
            {
                await repository.RollbackTransactionAsync(CancellationToken.None).ConfigureAwait(false);
                logger.LogError(exception, "Indexing failed for solution {SolutionPath}", solutionPath);
                result.Errors.Add(exception.Message);
                result.PartialSuccess = false;
            }
            finally
            {
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
            }

            logger.LogInformation("Completed solution indexing for snapshot {SnapshotId} in {Duration}ms", snapshotId, result.Duration.TotalMilliseconds);

            return result;
        }

        private async Task<IndexingResult> IndexProjectInternalAsync(long snapshotId, string projectPath, CancellationToken ct)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            IndexingResult result = new IndexingResult
            {
                SnapshotId = snapshotId
            };

            logger.LogInformation("Starting project indexing for snapshot {SnapshotId} at {ProjectPath}", snapshotId, projectPath);

            string repositoryRoot = DetermineRepositoryRootPath(projectPath);

            using WorkspaceLoader workspaceLoader = new WorkspaceLoader(loggerFactory.CreateLogger<WorkspaceLoader>());

            try
            {
                Project project = await workspaceLoader.LoadProjectAsync(projectPath, ct).ConfigureAwait(false);

                await repository.BeginTransactionAsync(ct).ConfigureAwait(false);
                await IndexProjectAsync(project, snapshotId, repositoryRoot, result, ct).ConfigureAwait(false);
                await repository.CommitTransactionAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await repository.RollbackTransactionAsync(CancellationToken.None).ConfigureAwait(false);
                result.Errors.Add("Indexing was cancelled.");
                result.PartialSuccess = true;
            }
            catch (Exception exception)
            {
                await repository.RollbackTransactionAsync(CancellationToken.None).ConfigureAwait(false);
                logger.LogError(exception, "Indexing failed for project {ProjectPath}", projectPath);
                result.Errors.Add(exception.Message);
                result.PartialSuccess = false;
            }
            finally
            {
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
            }

            logger.LogInformation("Completed project indexing for snapshot {SnapshotId} in {Duration}ms", snapshotId, result.Duration.TotalMilliseconds);

            return result;
        }

        private async Task IndexProjectAsync(Project project, long snapshotId, string repositoryRoot, IndexingResult result, CancellationToken ct)
        {
            Compilation? compilation = await project.GetCompilationAsync(ct).ConfigureAwait(false);

            if (compilation == null)
            {
                string message = $"Unable to create compilation for project {project.Name}.";
                logger.LogWarning(message);
                result.Errors.Add(message);
                result.PartialSuccess = true;
                return;
            }

            IEnumerable<Diagnostic> diagnostics = compilation.GetDiagnostics(ct);

            foreach (Diagnostic diagnostic in diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            {
                string message = $"Compilation error in {project.Name}: {diagnostic.GetMessage()}";
                logger.LogWarning(message);
                result.Errors.Add(message);
                result.PartialSuccess = true;
            }

            List<Document> documents = project.Documents.Where(document => !string.IsNullOrWhiteSpace(document.FilePath)).ToList();
            Dictionary<DocumentId, long> fileIds = new Dictionary<DocumentId, long>();

            foreach (Document document in documents)
            {
                if (document.FilePath == null)
                {
                    continue;
                }

                string relativePath = GetRelativePath(repositoryRoot, document.FilePath);
                long fileId = await repository.RecordFileAsync(snapshotId, relativePath, "csharp", null, ct).ConfigureAwait(false);
                fileIds[document.Id] = fileId;

                SyntaxTree? syntaxTree = await document.GetSyntaxTreeAsync(ct).ConfigureAwait(false);

                if (syntaxTree == null)
                {
                    continue;
                }

                SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);
                SyntaxNode root = await syntaxTree.GetRootAsync(ct).ConfigureAwait(false);

                SymbolDeclarationCollector symbolCollector = new SymbolDeclarationCollector(semanticModel, fileId, snapshotId, repository);
                symbolCollector.Visit(root);

                result.FilesIndexed++;
                result.SymbolsCollected += symbolCollector.SymbolCount;
            }

            foreach (Document document in documents)
            {
                if (document.FilePath == null)
                {
                    continue;
                }

                if (!fileIds.TryGetValue(document.Id, out long fileId))
                {
                    continue;
                }

                SyntaxTree? syntaxTree = await document.GetSyntaxTreeAsync(ct).ConfigureAwait(false);

                if (syntaxTree == null)
                {
                    continue;
                }

                SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);
                SyntaxNode root = await syntaxTree.GetRootAsync(ct).ConfigureAwait(false);

                ReferenceCollector referenceCollector = new ReferenceCollector(semanticModel, fileId, snapshotId, repository);
                referenceCollector.Visit(root);
                result.ReferencesCollected += referenceCollector.ReferenceCount;
            }

            logger.LogInformation("Indexed project {ProjectName}: {Files} files, {Symbols} symbols, {References} references",
                project.Name,
                result.FilesIndexed,
                result.SymbolsCollected,
                result.ReferencesCollected);
        }

        private async Task<IndexingResult> RunWithTimeoutAsync(Func<CancellationToken, Task<IndexingResult>> action, CancellationToken ct)
        {
            if (options.IndexingTimeoutMinutes <= 0)
            {
                return await action(ct).ConfigureAwait(false);
            }

            using CancellationTokenSource timeoutSource = new CancellationTokenSource(TimeSpan.FromMinutes(options.IndexingTimeoutMinutes));
            using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutSource.Token);

            return await action(linkedSource.Token).ConfigureAwait(false);
        }

        private string GetRelativePath(string repositoryRoot, string filePath)
        {
            if (string.IsNullOrWhiteSpace(repositoryRoot))
            {
                return Path.GetFileName(filePath);
            }

            string relativePath;

            try
            {
                relativePath = Path.GetRelativePath(repositoryRoot, filePath);
            }
            catch (Exception)
            {
                return Path.GetFileName(filePath);
            }

            if (relativePath.StartsWith("..", StringComparison.Ordinal))
            {
                return Path.GetFileName(filePath);
            }

            return relativePath;
        }

        private string DetermineRepositoryRootPath(string sourcePath)
        {
            string directoryPath = Path.GetDirectoryName(sourcePath) ?? sourcePath;
            DirectoryInfo? current = new DirectoryInfo(directoryPath);

            while (current != null)
            {
                string gitPath = Path.Combine(current.FullName, ".git");

                if (Directory.Exists(gitPath))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            return directoryPath;
        }

        private string? FindContainingProjectFile(string filePath)
        {
            DirectoryInfo? current = new DirectoryInfo(Path.GetDirectoryName(filePath) ?? string.Empty);

            while (current != null)
            {
                string[] projectFiles = Directory.GetFiles(current.FullName, "*.csproj", SearchOption.TopDirectoryOnly);

                if (projectFiles.Length > 0)
                {
                    return projectFiles[0];
                }

                current = current.Parent;
            }

            return null;
        }
    }
}

