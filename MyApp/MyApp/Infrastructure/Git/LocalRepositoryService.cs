using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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
                    bool hasUncommittedChanges = HasUncommittedChanges(repositoryDirectory.FullName);
                    bool hasUnpushedCommits = HasUnpushedCommits(repositoryDirectory.FullName);
                    LocalRepository repository = new LocalRepository(repositoryDirectory.Name, repositoryDirectory.FullName, remoteUrl, branches, hasUncommittedChanges, hasUnpushedCommits);
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
            NullRepositoryCloneProgress progress = new NullRepositoryCloneProgress();
            return CloneRepositoryInternalAsync(repositoryUrl, progress, CancellationToken.None).GetAwaiter().GetResult();
        }

        public Task<CloneRepositoryResult> CloneRepositoryAsync(string repositoryUrl, IProgress<RepositoryCloneProgress> progress, CancellationToken cancellationToken)
        {
            IProgress<RepositoryCloneProgress> safeProgress = progress ?? new NullRepositoryCloneProgress();
            return CloneRepositoryInternalAsync(repositoryUrl, safeProgress, cancellationToken);
        }

        public DeleteRepositoryResult DeleteRepository(string repositoryName)
        {
            if (string.IsNullOrWhiteSpace(repositoryName))
            {
                return new DeleteRepositoryResult(false, false, "The repository name must be provided.");
            }

            if (string.IsNullOrWhiteSpace(_options.RootPath))
            {
                string configurationMessage = "Repository root path is not configured.";
                _logger.LogWarning(configurationMessage);
                return new DeleteRepositoryResult(false, false, configurationMessage);
            }

            string rootFullPath;
            string candidatePath;

            try
            {
                rootFullPath = Path.GetFullPath(_options.RootPath);
                candidatePath = Path.GetFullPath(Path.Combine(_options.RootPath, repositoryName));
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to resolve repository path for {RepositoryName}", repositoryName);
                return new DeleteRepositoryResult(false, false, "The repository name is invalid.");
            }

            if (!candidatePath.StartsWith(rootFullPath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Attempted to delete repository outside of root: {RepositoryPath}", candidatePath);
                return new DeleteRepositoryResult(false, false, "The repository name is invalid.");
            }

            if (!Directory.Exists(candidatePath))
            {
                return new DeleteRepositoryResult(false, true, "The repository was not found.");
            }

            try
            {
                Directory.Delete(candidatePath, true);
            }
            catch (IOException exception)
            {
                _logger.LogError(exception, "Failed to delete repository at {RepositoryPath}", candidatePath);
                return new DeleteRepositoryResult(false, false, "Failed to delete the repository.");
            }
            catch (UnauthorizedAccessException exception)
            {
                _logger.LogError(exception, "Unauthorized to delete repository at {RepositoryPath}", candidatePath);
                return new DeleteRepositoryResult(false, false, "Failed to delete the repository.");
            }

            return new DeleteRepositoryResult(true, false, "Repository deleted.");
        }

        private async Task<CloneRepositoryResult> CloneRepositoryInternalAsync(string repositoryUrl, IProgress<RepositoryCloneProgress> progress, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(repositoryUrl))
            {
                throw new ArgumentException("The repository URL must be provided.", nameof(repositoryUrl));
            }

            if (string.IsNullOrWhiteSpace(_options.RootPath))
            {
                string message = "Repository root path is not configured.";
                _logger.LogWarning(message);
                return new CloneRepositoryResult(false, false, string.Empty, message, false);
            }

            try
            {
                EnsureRootDirectory();
            }
            catch (Exception exception)
            {
                string message = string.Format("Unable to create repository root at {0}.", _options.RootPath);
                _logger.LogError(exception, "Unable to create repository root at {RepositoryRoot}", _options.RootPath);
                return new CloneRepositoryResult(false, false, string.Empty, message, false);
            }

            string repositoryName;

            try
            {
                repositoryName = GetRepositoryName(repositoryUrl);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to determine repository name from {RepositoryUrl}", repositoryUrl);
                return new CloneRepositoryResult(false, false, string.Empty, "The repository URL is invalid.", false);
            }

            string repositoryPath = Path.Combine(_options.RootPath, repositoryName);

            if (Directory.Exists(repositoryPath))
            {
                RepositoryCloneProgress alreadyProgress = new RepositoryCloneProgress(100.0, "Repository already cloned.", string.Empty);
                progress.Report(alreadyProgress);
                return new CloneRepositoryResult(true, true, repositoryPath, "Repository already cloned.", false);
            }

            RepositoryCloneProgress startProgress = new RepositoryCloneProgress(0.0, "Starting clone", string.Empty);
            progress.Report(startProgress);

            CommandResult result;

            try
            {
                result = await ExecuteGitCloneAsync(_options.RootPath, repositoryUrl, repositoryPath, progress, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is OperationCanceledException || exception is TaskCanceledException)
            {
                TryDeleteDirectory(repositoryPath);
                string cancelledMessage = "Repository clone was canceled.";
                _logger.LogWarning("Git clone canceled for {RepositoryUrl}", repositoryUrl);
                return new CloneRepositoryResult(false, false, string.Empty, cancelledMessage, true);
            }

            if (result.WasCanceled || cancellationToken.IsCancellationRequested)
            {
                TryDeleteDirectory(repositoryPath);
                string cancelledMessage = "Repository clone was canceled.";
                _logger.LogWarning("Git clone canceled for {RepositoryUrl}", repositoryUrl);
                return new CloneRepositoryResult(false, false, string.Empty, cancelledMessage, true);
            }

            if (!result.Succeeded)
            {
                TryDeleteDirectory(repositoryPath);
                string trimmedError = result.StandardError.Trim();
                string message = string.IsNullOrWhiteSpace(trimmedError) ? "Failed to clone repository." : trimmedError;
                _logger.LogError("Git clone failed for {RepositoryUrl}: {Message}", repositoryUrl, message);
                return new CloneRepositoryResult(false, false, string.Empty, message, false);
            }

            RepositoryCloneProgress completedProgress = new RepositoryCloneProgress(100.0, "Completed", string.Empty);
            progress.Report(completedProgress);
            return new CloneRepositoryResult(true, false, repositoryPath, string.Empty, false);
        }

        private static async Task<CommandResult> ExecuteGitCloneAsync(string workingDirectory, string repositoryUrl, string repositoryPath, IProgress<RepositoryCloneProgress> progress, CancellationToken cancellationToken)
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

            startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
            startInfo.Environment["GIT_ASKPASS"] = "echo";
            startInfo.Environment["SSH_ASKPASS"] = "echo";

            startInfo.ArgumentList.Add("clone");
            startInfo.ArgumentList.Add("--progress");
            startInfo.ArgumentList.Add(repositoryUrl);
            startInfo.ArgumentList.Add(repositoryPath);

            StringBuilder standardOutputBuilder = new StringBuilder();
            StringBuilder standardErrorBuilder = new StringBuilder();
            double lastPercentage = 0.0;

            using (Process process = new Process())
            {
                process.StartInfo = startInfo;

                process.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        standardOutputBuilder.AppendLine(args.Data);
                    }
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        string trimmed = args.Data.Trim();
                        standardErrorBuilder.AppendLine(args.Data);

                        if (progress != null)
                        {
                            string stage;
                            double? percentage;

                            if (TryParseCloneProgress(trimmed, out stage, out percentage))
                            {
                                if (percentage.HasValue)
                                {
                                    lastPercentage = percentage.Value;
                                }

                                RepositoryCloneProgress update = new RepositoryCloneProgress(lastPercentage, stage, trimmed);
                                progress.Report(update);
                            }
                        }
                    }
                };

                TaskCompletionSource<bool> exitCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                EventHandler exitHandler = (sender, args) => exitCompletion.TrySetResult(true);
                process.EnableRaisingEvents = true;
                process.Exited += exitHandler;

                try
                {
                    using (cancellationToken.Register(() =>
                    {
                        exitCompletion.TrySetCanceled(cancellationToken);

                        try
                        {
                            if (!process.HasExited)
                            {
                                TryTerminateProcess(process);
                            }
                        }
                        catch (InvalidOperationException)
                        {
                        }
                        catch (NotSupportedException)
                        {
                        }
                    }))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            string canceledOutput = standardOutputBuilder.ToString();
                            string canceledError = standardErrorBuilder.ToString();
                            return new CommandResult(false, canceledOutput, canceledError, true);
                        }

                        bool started = process.Start();

                        if (!started)
                        {
                            return new CommandResult(false, string.Empty, "Unable to start git process.", false);
                        }

                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        try
                        {
                            await exitCompletion.Task.ConfigureAwait(false);
                        }
                        catch (Exception exception) when (exception is OperationCanceledException || exception is TaskCanceledException)
                        {
                            process.WaitForExit();
                            string canceledOutput = standardOutputBuilder.ToString();
                            string canceledError = standardErrorBuilder.ToString();
                            return new CommandResult(false, canceledOutput, canceledError, true);
                        }

                        process.WaitForExit();

                        if (cancellationToken.IsCancellationRequested)
                        {
                            string canceledOutput = standardOutputBuilder.ToString();
                            string canceledError = standardErrorBuilder.ToString();
                            return new CommandResult(false, canceledOutput, canceledError, true);
                        }
                    }
                }
                finally
                {
                    process.Exited -= exitHandler;
                }

                string standardOutput = standardOutputBuilder.ToString();
                string standardError = standardErrorBuilder.ToString();
                bool success = process.ExitCode == 0;

                return new CommandResult(success, standardOutput, standardError, false);
            }
        }

        private static bool TryParseCloneProgress(string line, out string stage, out double? percentage)
        {
            stage = string.Empty;
            percentage = null;

            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            string trimmedLine = line.Trim();

            Match match = Regex.Match(trimmedLine, @"^(?<stage>[A-Za-z ]+):\s+(?<percent>\d+)%");

            if (match.Success)
            {
                stage = match.Groups["stage"].Value.Trim();
                string percentText = match.Groups["percent"].Value;
                double parsed;

                if (double.TryParse(percentText, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                {
                    percentage = parsed;
                }

                return true;
            }

            if (trimmedLine.EndsWith("done.", StringComparison.OrdinalIgnoreCase))
            {
                stage = trimmedLine;
                percentage = 100.0;
                return true;
            }

            stage = trimmedLine;
            return true;
        }

        private static bool HasUncommittedChanges(string repositoryPath)
        {
            CommandResult statusResult = ExecuteGitCommand(repositoryPath, new[] { "status", "--porcelain" });

            if (!statusResult.Succeeded)
            {
                return false;
            }

            string output = statusResult.StandardOutput;

            if (string.IsNullOrWhiteSpace(output))
            {
                return false;
            }

            string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return lines.Length > 0;
        }

        private static bool HasUnpushedCommits(string repositoryPath)
        {
            CommandResult branchResult = ExecuteGitCommand(repositoryPath, new[] { "status", "--porcelain=1", "--branch" });

            if (!branchResult.Succeeded)
            {
                return false;
            }

            string[] lines = branchResult.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length == 0)
            {
                return false;
            }

            string branchLine = lines[0];
            Match match = Regex.Match(branchLine, @"ahead\s+(?<count>\d+)", RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                return false;
            }

            string countText = match.Groups["count"].Value;
            int parsedCount;

            if (!int.TryParse(countText, out parsedCount))
            {
                return false;
            }

            return parsedCount > 0;
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
                    return new CommandResult(false, string.Empty, "Unable to start git process.", false);
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
                        return new CommandResult(false, timeoutOutput, message, false);
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

                return new CommandResult(success, standardOutput, standardError, false);
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

        private sealed class NullRepositoryCloneProgress : IProgress<RepositoryCloneProgress>
        {
            public void Report(RepositoryCloneProgress value)
            {
            }
        }

        private readonly struct CommandResult
        {
            public CommandResult(bool succeeded, string standardOutput, string standardError, bool wasCanceled)
            {
                Succeeded = succeeded;
                StandardOutput = standardOutput;
                StandardError = standardError;
                WasCanceled = wasCanceled;
            }

            public bool Succeeded { get; }

            public string StandardOutput { get; }

            public string StandardError { get; }

            public bool WasCanceled { get; }
        }
    }
}
