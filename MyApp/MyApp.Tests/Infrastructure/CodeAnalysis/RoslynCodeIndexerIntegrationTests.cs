#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Build.Locator;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyApp.Application.Configuration;
using MyApp.Application.CodeAnalysis.DTOs;
using MyApp.CodeAnalysis.Abstractions;
using MyApp.Data;
using MyApp.Domain.CodeAnalysis;
using MyApp.Infrastructure.CodeAnalysis;
using Xunit;

namespace MyApp.Tests.Infrastructure.CodeAnalysis
{
    public sealed class RoslynCodeIndexerIntegrationTests
    {
        [Fact]
        public async Task IndexProjectAsync_ShouldIndexNodesEdgesAndLocations()
        {
            EnsureMsBuildRegistered();
            await using TemporaryProject project = TemporaryProject.CreateMultiFileProject();
            await using SqliteDbContextHandle dbHandle = await SqliteDbContextHandle.CreateAsync();
            CodeGraphRepository repository = new CodeGraphRepository(dbHandle.Context);
            RoslynCodeIndexer indexer = CreateIndexer(repository);

            long snapshotId = await repository.CreateRepositorySnapshotAsync("test-repo", project.RootPath, null, null, CancellationToken.None);
            IndexingResult result = await indexer.IndexProjectAsync(snapshotId, project.ProjectPath, CancellationToken.None);

            result.PartialSuccess.Should().BeFalse();
            result.FilesIndexed.Should().Be(2);
            dbHandle.Context.CodeNodes.Count().Should().BeGreaterThan(0);
            dbHandle.Context.CodeEdges.Any(edge => edge.Type == CSharpReferenceKind.Call).Should().BeTrue();
            dbHandle.Context.SourceLocations.Count().Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task IndexProjectAsync_ShouldHandleEmptyProject()
        {
            EnsureMsBuildRegistered();
            await using TemporaryProject project = TemporaryProject.CreateEmptyProject();
            await using SqliteDbContextHandle dbHandle = await SqliteDbContextHandle.CreateAsync();
            CodeGraphRepository repository = new CodeGraphRepository(dbHandle.Context);
            RoslynCodeIndexer indexer = CreateIndexer(repository);

            long snapshotId = await repository.CreateRepositorySnapshotAsync("empty-repo", project.RootPath, null, null, CancellationToken.None);
            IndexingResult result = await indexer.IndexProjectAsync(snapshotId, project.ProjectPath, CancellationToken.None);

            result.PartialSuccess.Should().BeFalse();
            result.FilesIndexed.Should().Be(0);
            dbHandle.Context.CodeNodes.Count().Should().Be(0);
            dbHandle.Context.CodeEdges.Count().Should().Be(0);
            dbHandle.Context.SourceLocations.Count().Should().Be(0);
        }

        [Fact]
        public async Task IndexProjectAsync_ShouldReportCompilationErrorsAndContinue()
        {
            EnsureMsBuildRegistered();
            await using TemporaryProject project = TemporaryProject.CreateProjectWithCompilationError();
            await using SqliteDbContextHandle dbHandle = await SqliteDbContextHandle.CreateAsync();
            CodeGraphRepository repository = new CodeGraphRepository(dbHandle.Context);
            RoslynCodeIndexer indexer = CreateIndexer(repository);

            long snapshotId = await repository.CreateRepositorySnapshotAsync("error-repo", project.RootPath, null, null, CancellationToken.None);
            IndexingResult result = await indexer.IndexProjectAsync(snapshotId, project.ProjectPath, CancellationToken.None);

            result.PartialSuccess.Should().BeTrue();
            result.Errors.Should().NotBeEmpty();
            dbHandle.Context.CodeNodes.Count().Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task IndexProjectAsync_ShouldReportFailure_WhenSdkIsMissing()
        {
            EnsureMsBuildRegistered();
            await using TemporaryProject project = TemporaryProject.CreateMultiFileProject();
            await using SqliteDbContextHandle dbHandle = await SqliteDbContextHandle.CreateAsync();
            CodeGraphRepository repository = new CodeGraphRepository(dbHandle.Context);
            RoslynCodeIndexer indexer = CreateIndexer(repository);

            string? originalSdkPath = Environment.GetEnvironmentVariable("MSBuildSDKsPath");
            string missingSdkPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("MSBuildSDKsPath", missingSdkPath);

            try
            {
                long snapshotId = await repository.CreateRepositorySnapshotAsync("missing-sdk-repo", project.RootPath, null, null, CancellationToken.None);
                IndexingResult result = await indexer.IndexProjectAsync(snapshotId, project.ProjectPath, CancellationToken.None);

                result.PartialSuccess.Should().BeTrue();
                result.Errors.Should().NotBeEmpty();
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBuildSDKsPath", originalSdkPath);
            }
        }

        private static RoslynCodeIndexer CreateIndexer(CodeGraphRepository repository)
        {
            CodeAnalysisOptions options = new CodeAnalysisOptions
            {
                IndexingTimeoutMinutes = 2
            };
            IOptions<CodeAnalysisOptions> optionsWrapper = Options.Create(options);
            return new RoslynCodeIndexer(repository, NullLogger<RoslynCodeIndexer>.Instance, optionsWrapper, NullLoggerFactory.Instance);
        }

        private static void EnsureMsBuildRegistered()
        {
            if (MSBuildLocator.IsRegistered)
            {
                return;
            }

            VisualStudioInstance[] instances = MSBuildLocator.QueryVisualStudioInstances().ToArray();

            if (instances.Length == 0)
            {
                throw new InvalidOperationException("MSBuild instance not found.");
            }

            VisualStudioInstance latestInstance = instances.OrderByDescending(instance => instance.Version).First();
            MSBuildLocator.RegisterInstance(latestInstance);
        }

        private sealed class SqliteDbContextHandle : IAsyncDisposable
        {
            private readonly SqliteConnection connection;

            private SqliteDbContextHandle(ApplicationDbContext context, SqliteConnection connection)
            {
                Context = context;
                this.connection = connection;
            }

            public ApplicationDbContext Context { get; }

            public static async Task<SqliteDbContextHandle> CreateAsync()
            {
                SqliteConnection connection = new SqliteConnection("Data Source=:memory:");
                await connection.OpenAsync().ConfigureAwait(false);

                DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseSqlite(connection)
                    .Options;

                ApplicationDbContext context = new ApplicationDbContext(options);
                await context.Database.EnsureCreatedAsync().ConfigureAwait(false);
                return new SqliteDbContextHandle(context, connection);
            }

            public async ValueTask DisposeAsync()
            {
                await Context.DisposeAsync().ConfigureAwait(false);
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }

        private sealed class TemporaryProject : IAsyncDisposable
        {
            private TemporaryProject(string rootPath, string projectPath)
            {
                RootPath = rootPath;
                ProjectPath = projectPath;
            }

            public string RootPath { get; }

            public string ProjectPath { get; }

            public static TemporaryProject CreateMultiFileProject()
            {
                TemporaryProject project = CreateProject("SampleProject.csproj", GetStandardProjectContents());
                project.WriteFile("Calculator.cs", GetCalculatorSource());
                project.WriteFile("Worker.cs", GetWorkerSource());
                return project;
            }

            public static TemporaryProject CreateEmptyProject()
            {
                TemporaryProject project = CreateProject("EmptyProject.csproj", GetEmptyProjectContents());
                return project;
            }

            public static TemporaryProject CreateProjectWithCompilationError()
            {
                TemporaryProject project = CreateProject("BrokenProject.csproj", GetStandardProjectContents());
                project.WriteFile("Broken.cs", GetBrokenSource());
                project.WriteFile("Helper.cs", GetHelperSource());
                return project;
            }

            public async ValueTask DisposeAsync()
            {
                await Task.Run(() => Directory.Delete(RootPath, true)).ConfigureAwait(false);
            }

            private static TemporaryProject CreateProject(string projectFileName, string projectContents)
            {
                string rootPath = Path.Combine(Path.GetTempPath(), "MyApp-Indexing-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(rootPath);
                string projectPath = Path.Combine(rootPath, projectFileName);
                File.WriteAllText(projectPath, projectContents);
                return new TemporaryProject(rootPath, projectPath);
            }

            private void WriteFile(string relativePath, string contents)
            {
                string fullPath = Path.Combine(RootPath, relativePath);
                File.WriteAllText(fullPath, contents);
            }

            private static string GetStandardProjectContents()
            {
                return JoinLines(
                    "<Project Sdk=\"Microsoft.NET.Sdk\">",
                    "  <PropertyGroup>",
                    "    <TargetFramework>net9.0</TargetFramework>",
                    "    <Nullable>enable</Nullable>",
                    "    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>",
                    "    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>",
                    "    <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>",
                    "  </PropertyGroup>",
                    "  <ItemGroup>",
                    "    <Compile Include=\"*.cs\" />",
                    "  </ItemGroup>",
                    "</Project>");
            }

            private static string GetEmptyProjectContents()
            {
                return JoinLines(
                    "<Project Sdk=\"Microsoft.NET.Sdk\">",
                    "  <PropertyGroup>",
                    "    <TargetFramework>net9.0</TargetFramework>",
                    "    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>",
                    "    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>",
                    "    <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>",
                    "  </PropertyGroup>",
                    "</Project>");
            }

            private static string GetCalculatorSource()
            {
                return JoinLines(
                    "namespace SampleProject;",
                    "",
                    "public sealed class Calculator",
                    "{",
                    "    public int Add(int left, int right)",
                    "    {",
                    "        return left + right;",
                    "    }",
                    "",
                    "    public int AddWithLogger(ILogger logger)",
                    "    {",
                    "        logger.Log();",
                    "        return 1;",
                    "    }",
                    "}",
                    "",
                    "public interface ILogger",
                    "{",
                    "    void Log();",
                    "}");
            }

            private static string GetWorkerSource()
            {
                return JoinLines(
                    "namespace SampleProject;",
                    "",
                    "public sealed class Worker",
                    "{",
                    "    public int Run()",
                    "    {",
                    "        Calculator calculator = new Calculator();",
                    "        return calculator.Add(1, 2);",
                    "    }",
                    "}");
            }

            private static string GetBrokenSource()
            {
                return JoinLines(
                    "namespace SampleProject;",
                    "",
                    "public sealed class Broken",
                    "{",
                    "    public void Run()",
                    "    {",
                    "        int value = ;",
                    "    }",
                    "}");
            }

            private static string GetHelperSource()
            {
                return JoinLines(
                    "namespace SampleProject;",
                    "",
                    "public sealed class Helper",
                    "{",
                    "    public int Add(int left, int right)",
                    "    {",
                    "        return left + right;",
                    "    }",
                    "}");
            }

            private static string JoinLines(params string[] lines)
            {
                return string.Join("\r\n", lines) + "\r\n";
            }
        }
    }
}
