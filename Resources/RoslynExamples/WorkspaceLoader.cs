// ============================================================================
// REFERENCE FILE: MSBuild Workspace Loader
// This is a reference implementation for the junior developer.
// DO NOT use directly - adapt to your project structure.
// ============================================================================

using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MyApp.CodeAnalysis.Reference
{
    /// <summary>
    /// Loads .NET solutions and projects using MSBuild for Roslyn analysis.
    ///
    /// IMPORTANT SETUP NOTES:
    ///
    /// 1. You MUST call MSBuildLocator.RegisterDefaults() ONCE at application startup,
    ///    BEFORE creating any MSBuildWorkspace. This is typically done in Program.cs.
    ///
    /// 2. Required NuGet packages:
    ///    - Microsoft.CodeAnalysis.Workspaces.MSBuild
    ///    - Microsoft.CodeAnalysis.CSharp.Workspaces
    ///    - Microsoft.Build.Locator
    ///
    /// 3. The MSBuild SDK must be installed on the machine running this code.
    ///    This is typically included with .NET SDK installation.
    ///
    /// WHY MSBuildWorkspace?
    /// - It understands .csproj and .sln files
    /// - It resolves project references automatically
    /// - It handles NuGet package references
    /// - It builds the full compilation context needed for semantic analysis
    /// </summary>
    public class WorkspaceLoader : IDisposable
    {
        private MSBuildWorkspace? _workspace;
        private bool _disposed;

        /// <summary>
        /// Diagnostics collected during workspace loading.
        /// Check this after loading to see any issues.
        /// </summary>
        public List<WorkspaceDiagnostic> Diagnostics { get; } = new List<WorkspaceDiagnostic>();

        /// <summary>
        /// Static initializer - call this ONCE at application startup.
        /// </summary>
        public static void Initialize()
        {
            // This MUST be called before creating any MSBuildWorkspace
            // It locates the MSBuild installation on the machine
            if (!MSBuildLocator.IsRegistered)
            {
                // Get all available Visual Studio / SDK instances
                VisualStudioInstance[] instances = MSBuildLocator.QueryVisualStudioInstances().ToArray();

                if (instances.Length == 0)
                {
                    throw new InvalidOperationException(
                        "No MSBuild instances found. Please install .NET SDK or Visual Studio.");
                }

                // Use the latest version
                VisualStudioInstance latestInstance = instances
                    .OrderByDescending(i => i.Version)
                    .First();

                MSBuildLocator.RegisterInstance(latestInstance);

                Console.WriteLine($"Using MSBuild from: {latestInstance.MSBuildPath}");
                Console.WriteLine($"Version: {latestInstance.Version}");
            }
        }

        /// <summary>
        /// Creates a new workspace for loading solutions/projects.
        /// </summary>
        public void CreateWorkspace()
        {
            if (_workspace != null)
            {
                _workspace.Dispose();
            }

            _workspace = MSBuildWorkspace.Create();

            // Subscribe to workspace failures (helpful for debugging)
            _workspace.WorkspaceFailed += OnWorkspaceFailed;
        }

        private void OnWorkspaceFailed(object? sender, WorkspaceDiagnosticEventArgs e)
        {
            Diagnostics.Add(e.Diagnostic);

            // Log the diagnostic for debugging
            string level = e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure ? "ERROR" : "WARNING";
            Console.WriteLine($"[{level}] {e.Diagnostic.Message}");
        }

        /// <summary>
        /// Loads a solution file (.sln) and all its projects.
        /// </summary>
        /// <param name="solutionPath">Full path to the .sln file</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The loaded solution</returns>
        public async Task<Solution> LoadSolutionAsync(string solutionPath, CancellationToken ct = default)
        {
            if (_workspace == null)
            {
                CreateWorkspace();
            }

            // Validate the path
            if (!File.Exists(solutionPath))
            {
                throw new FileNotFoundException($"Solution file not found: {solutionPath}");
            }

            if (!solutionPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Expected a .sln file, got: {solutionPath}");
            }

            Console.WriteLine($"Loading solution: {solutionPath}");

            Solution solution = await _workspace!.OpenSolutionAsync(solutionPath, cancellationToken: ct);

            Console.WriteLine($"Loaded {solution.Projects.Count()} projects");

            return solution;
        }

        /// <summary>
        /// Loads a single project file (.csproj).
        /// </summary>
        /// <param name="projectPath">Full path to the .csproj file</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The loaded project</returns>
        public async Task<Project> LoadProjectAsync(string projectPath, CancellationToken ct = default)
        {
            if (_workspace == null)
            {
                CreateWorkspace();
            }

            // Validate the path
            if (!File.Exists(projectPath))
            {
                throw new FileNotFoundException($"Project file not found: {projectPath}");
            }

            if (!projectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Expected a .csproj file, got: {projectPath}");
            }

            Console.WriteLine($"Loading project: {projectPath}");

            Project project = await _workspace!.OpenProjectAsync(projectPath, cancellationToken: ct);

            Console.WriteLine($"Loaded project with {project.Documents.Count()} documents");

            return project;
        }

        /// <summary>
        /// Finds all solution files in a directory.
        /// </summary>
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

        /// <summary>
        /// Finds all C# project files in a directory.
        /// </summary>
        public static IEnumerable<string> FindProjectFiles(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                yield break;
            }

            foreach (string file in Directory.EnumerateFiles(directoryPath, "*.csproj", SearchOption.AllDirectories))
            {
                // Skip common test/sample directories
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
                    file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                {
                    continue;
                }

                yield return file;
            }
        }

        /// <summary>
        /// Gets the compilation for a project.
        /// The compilation is required for semantic analysis.
        /// </summary>
        public static async Task<Compilation?> GetCompilationAsync(Project project, CancellationToken ct = default)
        {
            Compilation? compilation = await project.GetCompilationAsync(ct);

            if (compilation != null)
            {
                // Check for compilation errors
                IEnumerable<Diagnostic> errors = compilation.GetDiagnostics()
                    .Where(d => d.Severity == DiagnosticSeverity.Error);

                int errorCount = errors.Count();
                if (errorCount > 0)
                {
                    Console.WriteLine($"Warning: Project {project.Name} has {errorCount} compilation errors");

                    // Log first few errors
                    foreach (Diagnostic error in errors.Take(5))
                    {
                        Console.WriteLine($"  {error.Id}: {error.GetMessage()}");
                    }
                }
            }

            return compilation;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _workspace?.Dispose();
                    _workspace = null;
                }

                _disposed = true;
            }
        }
    }

    // ========================================================================
    // EXAMPLE USAGE IN PROGRAM.CS
    // ========================================================================

    /*
    // In your Program.cs, add this at the very beginning:

    public class Program
    {
        public static void Main(string[] args)
        {
            // Initialize MSBuild FIRST, before anything else
            WorkspaceLoader.Initialize();

            // Then continue with your normal startup...
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            // ...
        }
    }
    */

    // ========================================================================
    // EXAMPLE SERVICE THAT USES THE WORKSPACE LOADER
    // ========================================================================

    public class CodeIndexingService
    {
        private readonly ICodeGraphRepository _repository;

        public CodeIndexingService(ICodeGraphRepository repository)
        {
            _repository = repository;
        }

        public async Task IndexRepositoryAsync(string repositoryPath, CancellationToken ct)
        {
            using WorkspaceLoader loader = new WorkspaceLoader();

            // Find solutions in the repository
            IEnumerable<string> solutionFiles = WorkspaceLoader.FindSolutionFiles(repositoryPath);
            string? solutionFile = solutionFiles.FirstOrDefault();

            if (solutionFile != null)
            {
                // Index the solution
                Solution solution = await loader.LoadSolutionAsync(solutionFile, ct);
                await IndexSolutionAsync(solution, ct);
            }
            else
            {
                // No solution found, try to find individual projects
                IEnumerable<string> projectFiles = WorkspaceLoader.FindProjectFiles(repositoryPath);

                foreach (string projectFile in projectFiles)
                {
                    Project project = await loader.LoadProjectAsync(projectFile, ct);
                    await IndexProjectAsync(project, ct);
                }
            }
        }

        private async Task IndexSolutionAsync(Solution solution, CancellationToken ct)
        {
            foreach (Project project in solution.Projects)
            {
                await IndexProjectAsync(project, ct);
            }
        }

        private async Task IndexProjectAsync(Project project, CancellationToken ct)
        {
            // Get the compilation
            Compilation? compilation = await WorkspaceLoader.GetCompilationAsync(project, ct);

            if (compilation == null)
            {
                Console.WriteLine($"Skipping project {project.Name}: Could not get compilation");
                return;
            }

            // FIRST PASS: Collect all symbol declarations
            foreach (Document document in project.Documents)
            {
                if (document.FilePath == null) continue;

                // Get syntax tree and semantic model
                SyntaxTree? syntaxTree = await document.GetSyntaxTreeAsync(ct);
                if (syntaxTree == null) continue;

                SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);

                // Record the file
                int fileId = _repository.RecordFile(document.FilePath, "csharp");

                // Create the declaration collector and walk the syntax tree
                SymbolDeclarationCollector declarationCollector = new SymbolDeclarationCollector(
                    semanticModel,
                    fileId,
                    _repository);

                SyntaxNode root = await syntaxTree.GetRootAsync(ct);
                declarationCollector.Visit(root);

                Console.WriteLine($"Collected {declarationCollector.SymbolCount} symbols from {Path.GetFileName(document.FilePath)}");
            }

            // SECOND PASS: Collect all references
            foreach (Document document in project.Documents)
            {
                if (document.FilePath == null) continue;

                SyntaxTree? syntaxTree = await document.GetSyntaxTreeAsync(ct);
                if (syntaxTree == null) continue;

                SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);

                int fileId = _repository.GetFileId(document.FilePath);

                ReferenceCollector referenceCollector = new ReferenceCollector(
                    semanticModel,
                    fileId,
                    _repository);

                SyntaxNode root = await syntaxTree.GetRootAsync(ct);
                referenceCollector.Visit(root);

                Console.WriteLine($"Collected {referenceCollector.ReferenceCount} references from {Path.GetFileName(document.FilePath)}");
            }
        }
    }

    // Placeholder interface extension
    public partial interface ICodeGraphRepository
    {
        int RecordFile(string filePath, string language);
        int GetFileId(string filePath);
        void RecordOccurrence(int elementId, int fileId, int startLine, int startColumn, int endLine, int endColumn);
    }
}
