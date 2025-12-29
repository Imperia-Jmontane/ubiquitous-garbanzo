using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;

namespace MyApp.CodeAnalysis.Indexing
{
    public sealed class WorkspaceLoader : IDisposable
    {
        private readonly ILogger<WorkspaceLoader> logger;
        private MSBuildWorkspace? workspace;
        private bool disposed;

        public WorkspaceLoader(ILogger<WorkspaceLoader> logger)
        {
            this.logger = logger;
        }

        public Task<Solution> LoadSolutionAsync(string solutionPath, CancellationToken ct)
        {
            EnsureWorkspace();

            if (!File.Exists(solutionPath))
            {
                throw new FileNotFoundException($"Solution file not found: {solutionPath}");
            }

            if (!solutionPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Expected a .sln file, got: {solutionPath}");
            }

            logger.LogInformation("Loading solution {SolutionPath}", solutionPath);

            return workspace!.OpenSolutionAsync(solutionPath, cancellationToken: ct);
        }

        public Task<Project> LoadProjectAsync(string projectPath, CancellationToken ct)
        {
            EnsureWorkspace();

            if (!File.Exists(projectPath))
            {
                throw new FileNotFoundException($"Project file not found: {projectPath}");
            }

            if (!projectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Expected a .csproj file, got: {projectPath}");
            }

            logger.LogInformation("Loading project {ProjectPath}", projectPath);

            return workspace!.OpenProjectAsync(projectPath, cancellationToken: ct);
        }

        public static IEnumerable<string> FindSolutionFiles(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                yield break;
            }

            foreach (string file in Directory.EnumerateFiles(directoryPath, "*.sln", SearchOption.AllDirectories))
            {
                yield return file;
            }
        }

        public static IEnumerable<string> FindProjectFiles(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                yield break;
            }

            foreach (string file in Directory.EnumerateFiles(directoryPath, "*.csproj", SearchOption.AllDirectories))
            {
                yield return file;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing && workspace != null)
            {
                workspace.WorkspaceFailed -= OnWorkspaceFailed;
                workspace.Dispose();
            }

            workspace = null;
            disposed = true;
        }

        private void EnsureWorkspace()
        {
            if (workspace != null)
            {
                return;
            }

            workspace = MSBuildWorkspace.Create();
            workspace.WorkspaceFailed += OnWorkspaceFailed;
        }

        private void OnWorkspaceFailed(object? sender, WorkspaceDiagnosticEventArgs e)
        {
            if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
            {
                logger.LogError("Workspace error: {Message}", e.Diagnostic.Message);
                return;
            }

            logger.LogWarning("Workspace warning: {Message}", e.Diagnostic.Message);
        }
    }
}
