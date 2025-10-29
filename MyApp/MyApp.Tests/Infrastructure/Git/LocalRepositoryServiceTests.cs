#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyApp.Application.Configuration;
using MyApp.Domain.Repositories;
using MyApp.Infrastructure.Git;
using MyApp.Application.Abstractions;
using Xunit;

namespace MyApp.Tests.Infrastructure.Git
{
    public sealed class LocalRepositoryServiceTests : IDisposable
    {
        private readonly string _rootPath;

        public LocalRepositoryServiceTests()
        {
            _rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootPath);
        }

        [Fact]
        public void CloneRepository_ShouldCloneIntoRootPath()
        {
            string cloneRoot = Path.Combine(_rootPath, "clones");
            Directory.CreateDirectory(cloneRoot);

            RepositoryStorageOptions options = new RepositoryStorageOptions
            {
                RootPath = cloneRoot
            };

            ISecretProvider secretProvider = new NullSecretProvider();
            LocalRepositoryService service = new LocalRepositoryService(Options.Create(options), NullLogger<LocalRepositoryService>.Instance, secretProvider);

            string sourceRepositoryPath = Path.Combine(_rootPath, "source-repository");
            CreateRepository(sourceRepositoryPath, new List<string>(), string.Empty);

            CloneRepositoryResult cloneResult = service.CloneRepository(sourceRepositoryPath);

            Assert.True(cloneResult.Succeeded);
            Assert.False(cloneResult.AlreadyExists);
            string clonedRepositoryPath = Path.Combine(cloneRoot, "source-repository");
            Assert.True(Directory.Exists(clonedRepositoryPath));

            IReadOnlyCollection<LocalRepository> repositories = service.GetRepositories();
            Assert.Single(repositories);
            LocalRepository repository = repositories.Single();
            Assert.Equal("source-repository", repository.Name);
        }

        [Fact]
        public async Task CloneRepositoryAsync_ShouldReportProgress()
        {
            string cloneRoot = Path.Combine(_rootPath, "async-clones");
            Directory.CreateDirectory(cloneRoot);

            RepositoryStorageOptions options = new RepositoryStorageOptions
            {
                RootPath = cloneRoot
            };

            ISecretProvider secretProvider = new NullSecretProvider();
            LocalRepositoryService service = new LocalRepositoryService(Options.Create(options), NullLogger<LocalRepositoryService>.Instance, secretProvider);

            string sourceRepositoryPath = Path.Combine(_rootPath, "source-repository-async");
            CreateRepository(sourceRepositoryPath, new List<string>(), string.Empty);

            List<RepositoryCloneProgress> updates = new List<RepositoryCloneProgress>();
            Progress<RepositoryCloneProgress> progress = new Progress<RepositoryCloneProgress>(update =>
            {
                updates.Add(update);
            });

            CloneRepositoryResult result = await service.CloneRepositoryAsync(sourceRepositoryPath, progress, CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.False(result.AlreadyExists);
            Assert.NotEmpty(updates);
        }

        [Fact]
        public void GetRepositories_ShouldReturnRepositoriesWithBranches()
        {
            string firstRepositoryPath = Path.Combine(_rootPath, "ubiquitous-garbanzo");
            CreateRepository(firstRepositoryPath, new List<string> { "develop" }, "https://github.com/Imperia-Jmontane/ubiquitous-garbanzo");

            string secondRepositoryPath = Path.Combine(_rootPath, "vn2-inventory-optimization");
            CreateRepository(secondRepositoryPath, new List<string> { "feature/planning", "release/2024-06" }, "https://github.com/imperia-scm/vn2-inventory-optimization.git");

            RepositoryStorageOptions options = new RepositoryStorageOptions
            {
                RootPath = _rootPath
            };

            ISecretProvider secretProvider = new NullSecretProvider();
            LocalRepositoryService service = new LocalRepositoryService(Options.Create(options), NullLogger<LocalRepositoryService>.Instance, secretProvider);

            IReadOnlyCollection<LocalRepository> repositories = service.GetRepositories();

            Assert.Equal(2, repositories.Count);

            LocalRepository firstRepository = repositories.Single(repository => repository.Name == "ubiquitous-garbanzo");
            Assert.Equal("https://github.com/Imperia-Jmontane/ubiquitous-garbanzo", firstRepository.RemoteUrl);
            Assert.Contains(firstRepository.Branches, branch => branch.Name == "develop");
            Assert.Contains(firstRepository.Branches, branch => branch.Name == "main");
            Assert.True(firstRepository.HasRemote);
            Assert.False(firstRepository.HasUncommittedChanges);
            Assert.False(firstRepository.HasUnpushedCommits);

            LocalRepository secondRepository = repositories.Single(repository => repository.Name == "vn2-inventory-optimization");
            Assert.Equal("https://github.com/imperia-scm/vn2-inventory-optimization.git", secondRepository.RemoteUrl);
            Assert.Contains(secondRepository.Branches, branch => branch.Name == "feature/planning");
            Assert.Contains(secondRepository.Branches, branch => branch.Name == "release/2024-06");
            Assert.True(secondRepository.HasRemote);
            Assert.False(secondRepository.HasUncommittedChanges);
            Assert.False(secondRepository.HasUnpushedCommits);
        }

        [Fact]
        public void FetchRepository_ShouldReturnErrorForInvalidPath()
        {
            RepositoryStorageOptions options = new RepositoryStorageOptions
            {
                RootPath = _rootPath
            };

            ISecretProvider secretProvider = new NullSecretProvider();
            LocalRepositoryService service = new LocalRepositoryService(Options.Create(options), NullLogger<LocalRepositoryService>.Instance, secretProvider);

            string invalidPath = Path.Combine(_rootPath, "missing-repository");
            GitCommandResult result = service.FetchRepository(invalidPath);

            Assert.False(result.Succeeded);
            Assert.False(string.IsNullOrWhiteSpace(result.Message));
        }

        [Fact]
        public void RepositoryCommands_ShouldExecuteSuccessfully()
        {
            string workspacePath = Path.Combine(_rootPath, "workspace");
            Directory.CreateDirectory(workspacePath);

            RepositoryStorageOptions options = new RepositoryStorageOptions
            {
                RootPath = workspacePath
            };

            ISecretProvider secretProvider = new NullSecretProvider();
            LocalRepositoryService service = new LocalRepositoryService(Options.Create(options), NullLogger<LocalRepositoryService>.Instance, secretProvider);

            ExecuteGit(_rootPath, new[] { "init", "--bare", "remote.git" });

            string remotePath = Path.Combine(_rootPath, "remote.git");
            string producerPath = Path.Combine(_rootPath, "producer");
            CreateRepository(producerPath, new List<string>(), string.Empty);
            ExecuteGit(producerPath, new[] { "remote", "add", "origin", remotePath });
            ExecuteGit(producerPath, new[] { "push", "-u", "origin", "main" });
            ExecuteGit(_rootPath, new[] { "--git-dir", remotePath, "symbolic-ref", "HEAD", "refs/heads/main" });

            ExecuteGit(workspacePath, new[] { "clone", remotePath, "local" });
            string localRepositoryPath = Path.Combine(workspacePath, "local");

            GitCommandResult fetchResult = service.FetchRepository(localRepositoryPath);
            Assert.True(fetchResult.Succeeded, fetchResult.Message);

            string remoteUpdatePath = Path.Combine(producerPath, "REMOTE.md");
            File.WriteAllText(remoteUpdatePath, "Remote change");
            ExecuteGit(producerPath, new[] { "add", "." });
            ExecuteGit(producerPath, new[] { "commit", "-m", "Add remote change" });
            ExecuteGit(producerPath, new[] { "push", "origin", "main" });
            string expectedRemoteHead = ReadGitOutput(producerPath, new[] { "rev-parse", "HEAD" });

            GitCommandResult pullResult = service.PullRepository(localRepositoryPath);
            Assert.True(pullResult.Succeeded, pullResult.Message);
            string localHeadAfterPull = ReadGitOutput(localRepositoryPath, new[] { "rev-parse", "HEAD" });
            Assert.Equal(expectedRemoteHead, localHeadAfterPull);

            string localUpdatePath = Path.Combine(localRepositoryPath, "LOCAL.md");
            File.WriteAllText(localUpdatePath, "Local change");
            ExecuteGit(localRepositoryPath, new[] { "config", "user.email", "tests@example.com" });
            ExecuteGit(localRepositoryPath, new[] { "config", "user.name", "Tests" });
            GitCommandResult commitResult = service.CommitRepository(localRepositoryPath);
            Assert.True(commitResult.Succeeded, commitResult.Message);
            string latestCommitMessage = ReadGitOutput(localRepositoryPath, new[] { "log", "-1", "--pretty=%B" });
            Assert.Equal("Commited from Flow", latestCommitMessage);

            GitCommandResult pushResult = service.PushRepository(localRepositoryPath);
            Assert.True(pushResult.Succeeded, pushResult.Message);

            ExecuteGit(localRepositoryPath, new[] { "checkout", "-b", "feature/publish" });
            string featureFilePath = Path.Combine(localRepositoryPath, "FEATURE.md");
            File.WriteAllText(featureFilePath, "Feature branch work");
            ExecuteGit(localRepositoryPath, new[] { "add", "." });
            ExecuteGit(localRepositoryPath, new[] { "commit", "-m", "Add feature branch" });

            GitCommandResult publishResult = service.PublishBranch(localRepositoryPath, "feature/publish");
            Assert.True(publishResult.Succeeded, publishResult.Message);

            string featureBranchHead = ReadGitOutput(_rootPath, new[] { "--git-dir", remotePath, "rev-parse", "refs/heads/feature/publish" });
            string localFeatureHead = ReadGitOutput(localRepositoryPath, new[] { "rev-parse", "HEAD" });
            Assert.Equal(localFeatureHead, featureBranchHead);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_rootPath))
                {
                    Directory.Delete(_rootPath, true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void CreateRepository(string repositoryPath, IReadOnlyCollection<string> branches, string remoteUrl)
        {
            Directory.CreateDirectory(repositoryPath);

            ExecuteGit(repositoryPath, new[] { "init" });
            ExecuteGit(repositoryPath, new[] { "config", "user.email", "tests@example.com" });
            ExecuteGit(repositoryPath, new[] { "config", "user.name", "Tests" });

            string readmePath = Path.Combine(repositoryPath, "README.md");
            File.WriteAllText(readmePath, "# Test repository");
            ExecuteGit(repositoryPath, new[] { "add", "." });
            ExecuteGit(repositoryPath, new[] { "commit", "-m", "Initial commit" });
            ExecuteGit(repositoryPath, new[] { "branch", "-M", "main" });

            foreach (string branch in branches)
            {
                if (string.Equals(branch, "main", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ExecuteGit(repositoryPath, new[] { "branch", branch });
            }

            if (!string.IsNullOrWhiteSpace(remoteUrl))
            {
                ExecuteGit(repositoryPath, new[] { "remote", "add", "origin", remoteUrl });
            }
        }

        private static void ExecuteGit(string workingDirectory, IReadOnlyCollection<string> arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = workingDirectory,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                bool started = process.Start();

                if (!started)
                {
                    throw new InvalidOperationException("Unable to start git process.");
                }

                string standardOutput = process.StandardOutput.ReadToEnd();
                string standardError = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string message = string.Format("Git command failed: {0}", standardError);
                    throw new InvalidOperationException(message);
                }
            }
        }

        private static string ReadGitOutput(string workingDirectory, IReadOnlyCollection<string> arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = workingDirectory,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                bool started = process.Start();

                if (!started)
                {
                    throw new InvalidOperationException("Unable to start git process.");
                }

                string standardOutput = process.StandardOutput.ReadToEnd();
                string standardError = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string message = string.Format("Git command failed: {0}", standardError);
                    throw new InvalidOperationException(message);
                }

                return standardOutput.Trim();
            }
        }

        private sealed class NullSecretProvider : ISecretProvider
        {
            public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken)
            {
                return Task.FromResult<string?>(null);
            }
        }
    }
}
