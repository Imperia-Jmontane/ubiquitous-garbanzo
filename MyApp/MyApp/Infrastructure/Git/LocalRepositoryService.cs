using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyApp.Application.Abstractions;
using MyApp.Application.Configuration;
using MyApp.Domain.Repositories;

namespace MyApp.Infrastructure.Git
{
    public sealed class LocalRepositoryService : ILocalRepositoryService
    {
        private readonly RepositoryStorageOptions _options;
        private readonly ILogger<LocalRepositoryService> _logger;

        public LocalRepositoryService(IOptions<RepositoryStorageOptions> options, ILogger<LocalRepositoryService> logger)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _options = options.Value;
            _logger = logger;
        }

        public IReadOnlyCollection<LocalRepository> GetRepositories()
        {
            List<LocalRepository> repositories = new List<LocalRepository>();

            if (string.IsNullOrWhiteSpace(_options.RootPath))
            {
                return repositories;
            }

            DirectoryInfo rootDirectory = new DirectoryInfo(_options.RootPath);

            if (!rootDirectory.Exists)
            {
                return repositories;
            }

            DirectoryInfo[] repositoryDirectories = rootDirectory.GetDirectories();

            foreach (DirectoryInfo repositoryDirectory in repositoryDirectories)
            {
                string gitDirectoryPath = Path.Combine(repositoryDirectory.FullName, ".git");

                if (!Directory.Exists(gitDirectoryPath))
                {
                    continue;
                }

                try
                {
                    IReadOnlyCollection<string> branches = GetBranches(repositoryDirectory.FullName);
                    string remoteUrl = GetRemoteUrl(repositoryDirectory.FullName);
                    LocalRepository repository = new LocalRepository(repositoryDirectory.Name, repositoryDirectory.FullName, remoteUrl, branches);
                    repositories.Add(repository);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Failed to load repository information from {RepositoryPath}", repositoryDirectory.FullName);
                }
            }

            return repositories
                .OrderBy(repository => repository.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public CloneRepositoryResult CloneRepository(string repositoryUrl)
        {
            if (string.IsNullOrWhiteSpace(repositoryUrl))
            {
                throw new ArgumentException("The repository URL must be provided.", nameof(repositoryUrl));
            }

            if (string.IsNullOrWhiteSpace(_options.RootPath))
            {
                string message = "Repository root path is not configured.";
                _logger.LogWarning(message);
                return new CloneRepositoryResult(false, false, string.Empty, message);
            }

            try
            {
                EnsureRootDirectory();
            }
            catch (Exception exception)
            {
                string message = string.Format("Unable to create repository root at {0}.", _options.RootPath);
                _logger.LogError(exception, "Unable to create repository root at {RepositoryRoot}", _options.RootPath);
                return new CloneRepositoryResult(false, false, string.Empty, message);
            }

            string repositoryName;

            try
            {
                repositoryName = GetRepositoryName(repositoryUrl);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to determine repository name from {RepositoryUrl}", repositoryUrl);
                return new CloneRepositoryResult(false, false, string.Empty, "The repository URL is invalid.");
            }

            string repositoryPath = Path.Combine(_options.RootPath, repositoryName);

            if (Directory.Exists(repositoryPath))
            {
                return new CloneRepositoryResult(true, true, repositoryPath, "Repository already cloned.");
            }

            string[] arguments = new[] { "clone", repositoryUrl, repositoryPath };
            CommandResult result = ExecuteGitCommand(_options.RootPath, arguments, TimeSpan.FromMinutes(5));

            if (!result.Succeeded)
            {
                TryDeleteDirectory(repositoryPath);
                string trimmedError = result.StandardError.Trim();
                string message = string.IsNullOrWhiteSpace(trimmedError) ? "Failed to clone repository." : trimmedError;
                _logger.LogError("Git clone failed for {RepositoryUrl}: {Message}", repositoryUrl, message);
                return new CloneRepositoryResult(false, false, string.Empty, message);
            }

            return new CloneRepositoryResult(true, false, repositoryPath, string.Empty);
        }

        private static IReadOnlyCollection<string> GetBranches(string repositoryPath)
        {
            List<string> branches = new List<string>();
            string[] arguments = new[] { "branch", "--format=%(refname:short)" };
            CommandResult result = ExecuteGitCommand(repositoryPath, arguments);

            if (!result.Succeeded)
            {
                return branches;
            }

            string[] lines = result.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                string trimmed = line.Trim();

                if (!string.IsNullOrEmpty(trimmed))
                {
                    branches.Add(trimmed);
                }
            }

            return branches
                .OrderBy(branch => branch, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string GetRemoteUrl(string repositoryPath)
        {
            string[] arguments = new[] { "config", "--get", "remote.origin.url" };
            CommandResult result = ExecuteGitCommand(repositoryPath, arguments);

            if (!result.Succeeded)
            {
                return string.Empty;
            }

            return result.StandardOutput.Trim();
        }

        private static CommandResult ExecuteGitCommand(string repositoryPath, IReadOnlyCollection<string> arguments, TimeSpan? timeout = null)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = repositoryPath,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
            startInfo.Environment["GIT_ASKPASS"] = "echo";
            startInfo.Environment["SSH_ASKPASS"] = "echo";

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
                    return new CommandResult(false, string.Empty, "Unable to start git process.");
                }

                Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();

                bool exited;

                if (timeout.HasValue)
                {
                    exited = process.WaitForExit((int)timeout.Value.TotalMilliseconds);

                    if (!exited)
                    {
                        TryTerminateProcess(process);
                        Task.WaitAll(new Task[] { standardOutputTask, standardErrorTask }, TimeSpan.FromSeconds(2));
                        string timeoutOutput = standardOutputTask.IsCompletedSuccessfully ? standardOutputTask.Result : string.Empty;
                        string timeoutError = standardErrorTask.IsCompletedSuccessfully ? standardErrorTask.Result : string.Empty;
                        string message = string.IsNullOrWhiteSpace(timeoutError) ? "Git command timed out." : timeoutError.Trim();
                        return new CommandResult(false, timeoutOutput, message);
                    }
                }
                else
                {
                    process.WaitForExit();
                }

                Task.WaitAll(standardOutputTask, standardErrorTask);

                string standardOutput = standardOutputTask.Result;
                string standardError = standardErrorTask.Result;
                bool success = process.ExitCode == 0;

                return new CommandResult(success, standardOutput, standardError);
            }
        }

        private static string GetRepositoryName(string repositoryUrl)
        {
            string trimmedUrl = repositoryUrl.Trim();

            if (trimmedUrl.Length == 0)
            {
                throw new ArgumentException("Repository URL is empty.", nameof(repositoryUrl));
            }

            string normalizedUrl = trimmedUrl.TrimEnd('/', '\\');
            string[] segments = normalizedUrl.Split(new[] { '/', '\\', ':' }, StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length == 0)
            {
                throw new InvalidOperationException("Repository name could not be determined.");
            }

            string candidate = segments[segments.Length - 1];

            if (candidate.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            {
                candidate = candidate.Substring(0, candidate.Length - 4);
            }

            if (string.IsNullOrWhiteSpace(candidate))
            {
                throw new InvalidOperationException("Repository name could not be determined.");
            }

            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                if (candidate.IndexOf(invalidCharacter) >= 0)
                {
                    throw new InvalidOperationException("Repository name contains invalid characters.");
                }
            }

            return candidate;
        }

        private void EnsureRootDirectory()
        {
            if (Directory.Exists(_options.RootPath))
            {
                return;
            }

            Directory.CreateDirectory(_options.RootPath);
        }

        private static void TryDeleteDirectory(string repositoryPath)
        {
            if (!Directory.Exists(repositoryPath))
            {
                return;
            }

            try
            {
                Directory.Delete(repositoryPath, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void TryTerminateProcess(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                    process.WaitForExit();
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (NotSupportedException)
            {
            }
        }

        private readonly struct CommandResult
        {
            public CommandResult(bool succeeded, string standardOutput, string standardError)
            {
                Succeeded = succeeded;
                StandardOutput = standardOutput;
                StandardError = standardError;
            }

            public bool Succeeded { get; }

            public string StandardOutput { get; }

            public string StandardError { get; }
        }
    }
}
