using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Security;
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
        private readonly ISecretProvider _secretProvider;

        public LocalRepositoryService(IOptions<RepositoryStorageOptions> options, ILogger<LocalRepositoryService> logger, ISecretProvider secretProvider)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            if (secretProvider == null)
            {
                throw new ArgumentNullException(nameof(secretProvider));
            }

            _options = options.Value;
            _logger = logger;
            _secretProvider = secretProvider;
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
                    IReadOnlyCollection<RepositoryBranch> branches = GetBranches(repositoryDirectory.FullName);
                    string remoteUrl = GetRemoteUrl(repositoryDirectory.FullName);
                    RepositorySyncInfo syncInfo = GetRepositorySyncInfo(repositoryDirectory.FullName);
                    DateTimeOffset? lastFetchTimeUtc = GetLastFetchTimeUtc(repositoryDirectory.FullName);
                    LocalRepository repository = new LocalRepository(repositoryDirectory.Name, repositoryDirectory.FullName, remoteUrl, branches, syncInfo.HasUncommittedChanges, syncInfo.HasUnpushedCommits, lastFetchTimeUtc);
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

        public DeleteRepositoryResult DeleteRepository(string repositoryPath)
        {
            if (string.IsNullOrWhiteSpace(repositoryPath))
            {
                return new DeleteRepositoryResult(false, "The repository path must be provided.");
            }

            if (string.IsNullOrWhiteSpace(_options.RootPath))
            {
                string message = "Repository root path is not configured.";
                _logger.LogWarning(message);
                return new DeleteRepositoryResult(false, message);
            }

            string fullRootPath;
            string candidateRepositoryPath = repositoryPath;

            try
            {
                fullRootPath = Path.GetFullPath(_options.RootPath);

                if (!Path.IsPathRooted(candidateRepositoryPath))
                {
                    candidateRepositoryPath = Path.Combine(fullRootPath, candidateRepositoryPath);
                }

                candidateRepositoryPath = Path.GetFullPath(candidateRepositoryPath);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException || exception is SecurityException)
            {
                _logger.LogWarning(exception, "Invalid repository path provided for deletion: {RepositoryPath}", repositoryPath);
                return new DeleteRepositoryResult(false, "The repository path is invalid.");
            }

            if (!candidateRepositoryPath.StartsWith(fullRootPath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Repository path {RepositoryPath} is outside of the configured storage root.", candidateRepositoryPath);
                return new DeleteRepositoryResult(false, "The repository path is invalid.");
            }

            if (!Directory.Exists(candidateRepositoryPath))
            {
                return new DeleteRepositoryResult(false, "The repository could not be found.");
            }

            try
            {
                Directory.Delete(candidateRepositoryPath, true);
                return new DeleteRepositoryResult(true, string.Empty);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                _logger.LogError(exception, "Unable to delete repository at {RepositoryPath}", candidateRepositoryPath);
                return new DeleteRepositoryResult(false, "Unable to delete the repository. Please try again.");
            }
        }

        public bool RepositoryExists(string repositoryUrl)
        {
            if (string.IsNullOrWhiteSpace(repositoryUrl))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(_options.RootPath))
            {
                return false;
            }

            string repositoryName;

            try
            {
                repositoryName = GetRepositoryName(repositoryUrl);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                _logger.LogDebug(exception, "Unable to determine repository name for {RepositoryUrl} while checking existence.", repositoryUrl);
                return false;
            }

            string repositoryPath = Path.Combine(_options.RootPath, repositoryName);

            if (!Directory.Exists(repositoryPath))
            {
                return false;
            }

            string gitDirectoryPath = Path.Combine(repositoryPath, ".git");

            if (!Directory.Exists(gitDirectoryPath))
            {
                return false;
            }

            return true;
        }

        public GitCommandResult FetchRepository(string repositoryPath)
        {
            string[] arguments = new[] { "fetch", "--all", "--prune" };
            return ExecuteRepositoryCommand(repositoryPath, arguments, "Fetch completed successfully.", "Failed to fetch the repository.");
        }

        public GitCommandResult PullRepository(string repositoryPath)
        {
            string[] arguments = new[] { "pull", "--ff-only" };
            return ExecuteRepositoryCommand(repositoryPath, arguments, "Pull completed successfully.", "Failed to pull the repository.");
        }

        public GitCommandResult PushRepository(string repositoryPath)
        {
            string[] arguments = new[] { "push" };
            return ExecuteRepositoryCommand(repositoryPath, arguments, "Push completed successfully.", "Failed to push the repository.");
        }

        public GitCommandResult PublishBranch(string repositoryPath, string branchName)
        {
            if (string.IsNullOrWhiteSpace(branchName))
            {
                return new GitCommandResult(false, "The branch name must be provided.", string.Empty);
            }

            string[] arguments = new[] { "push", "--set-upstream", "origin", branchName };
            return ExecuteRepositoryCommand(repositoryPath, arguments, "Branch published successfully.", "Failed to publish the branch.");
        }

        public GitCommandResult SwitchBranch(string repositoryPath, string branchName)
        {
            if (string.IsNullOrWhiteSpace(branchName))
            {
                return new GitCommandResult(false, "The branch name must be provided.", string.Empty);
            }

            string resolvedRepositoryPath;
            string validationMessage;

            if (!TryResolveRepositoryPath(repositoryPath, out resolvedRepositoryPath, out validationMessage))
            {
                return new GitCommandResult(false, validationMessage, string.Empty);
            }

            IReadOnlyCollection<RepositoryBranch> existingBranches = GetBranches(resolvedRepositoryPath);
            bool branchExists = false;

            foreach (RepositoryBranch branch in existingBranches)
            {
                if (string.Equals(branch.Name, branchName, StringComparison.OrdinalIgnoreCase))
                {
                    branchExists = true;
                    break;
                }
            }

            string[] arguments;

            if (branchExists)
            {
                arguments = new[] { "switch", branchName };
            }
            else
            {
                string remoteReference = string.Format("origin/{0}", branchName);
                arguments = new[] { "switch", "--track", remoteReference };
            }

            return ExecuteResolvedRepositoryCommand(resolvedRepositoryPath, arguments, "Branch switched successfully.", "Failed to switch the branch.");
        }

        public GitCommandResult CommitRepository(string repositoryPath)
        {
            string resolvedRepositoryPath;
            string validationMessage;

            if (!TryResolveRepositoryPath(repositoryPath, out resolvedRepositoryPath, out validationMessage))
            {
                return new GitCommandResult(false, validationMessage, string.Empty);
            }

            string[] stageArguments = new[] { "add", "--all" };
            CommandResult stageResult = ExecuteGitCommand(resolvedRepositoryPath, stageArguments, TimeSpan.FromMinutes(2));

            if (!stageResult.Succeeded)
            {
                string stageCombinedOutput = string.IsNullOrWhiteSpace(stageResult.StandardError) ? stageResult.StandardOutput : stageResult.StandardError;
                string stageTrimmedError = string.IsNullOrWhiteSpace(stageCombinedOutput) ? string.Empty : stageCombinedOutput.Trim();
                string stageMessage = string.IsNullOrWhiteSpace(stageTrimmedError) ? "Failed to stage changes." : stageTrimmedError;
                string stageArgumentsDisplay = string.Join(" ", stageArguments);
                _logger.LogWarning("Git command {Arguments} failed for {RepositoryPath}: {Message}", stageArgumentsDisplay, resolvedRepositoryPath, stageMessage);
                return new GitCommandResult(false, stageMessage, stageTrimmedError);
            }

            string[] statusArguments = new[] { "status", "--porcelain" };
            CommandResult statusResult = ExecuteGitCommand(resolvedRepositoryPath, statusArguments, TimeSpan.FromMinutes(1));

            if (!statusResult.Succeeded)
            {
                string statusCombinedOutput = string.IsNullOrWhiteSpace(statusResult.StandardError) ? statusResult.StandardOutput : statusResult.StandardError;
                string statusTrimmedError = string.IsNullOrWhiteSpace(statusCombinedOutput) ? string.Empty : statusCombinedOutput.Trim();
                string statusMessage = string.IsNullOrWhiteSpace(statusTrimmedError) ? "Failed to evaluate repository status." : statusTrimmedError;
                string statusArgumentsDisplay = string.Join(" ", statusArguments);
                _logger.LogWarning("Git command {Arguments} failed for {RepositoryPath}: {Message}", statusArgumentsDisplay, resolvedRepositoryPath, statusMessage);
                return new GitCommandResult(false, statusMessage, statusTrimmedError);
            }

            string statusOutput = statusResult.StandardOutput;

            if (string.IsNullOrWhiteSpace(statusOutput) || string.IsNullOrWhiteSpace(statusOutput.Trim()))
            {
                return new GitCommandResult(false, "There are no changes to commit.", string.Empty);
            }

            string[] commitArguments = new[] { "commit", "-m", "Commited from Flow" };
            CommandResult commitResult = ExecuteGitCommand(resolvedRepositoryPath, commitArguments, TimeSpan.FromMinutes(2));

            if (!commitResult.Succeeded)
            {
                string commitCombinedOutput = string.IsNullOrWhiteSpace(commitResult.StandardError) ? commitResult.StandardOutput : commitResult.StandardError;
                string commitTrimmedError = string.IsNullOrWhiteSpace(commitCombinedOutput) ? string.Empty : commitCombinedOutput.Trim();
                string commitMessage = string.IsNullOrWhiteSpace(commitTrimmedError) ? "Failed to commit changes." : commitTrimmedError;
                string commitArgumentsDisplay = string.Join(" ", commitArguments);
                _logger.LogWarning("Git command {Arguments} failed for {RepositoryPath}: {Message}", commitArgumentsDisplay, resolvedRepositoryPath, commitMessage);
                return new GitCommandResult(false, commitMessage, commitTrimmedError);
            }

            string commitOutput = string.IsNullOrWhiteSpace(commitResult.StandardOutput) ? string.Empty : commitResult.StandardOutput.Trim();
            string successMessage = string.IsNullOrWhiteSpace(commitOutput) ? "Changes committed successfully." : commitOutput;
            return new GitCommandResult(true, successMessage, commitOutput);
        }

        public GitCommandResult DeleteBranch(string repositoryPath, string branchName)
        {
            if (string.IsNullOrWhiteSpace(branchName))
            {
                return new GitCommandResult(false, "The branch name must be provided.", string.Empty);
            }

            string resolvedRepositoryPath;
            string validationMessage;

            if (!TryResolveRepositoryPath(repositoryPath, out resolvedRepositoryPath, out validationMessage))
            {
                return new GitCommandResult(false, validationMessage, string.Empty);
            }

            IReadOnlyCollection<RepositoryBranch> existingBranches = GetBranches(resolvedRepositoryPath);
            RepositoryBranch? targetBranch = null;

            foreach (RepositoryBranch branch in existingBranches)
            {
                if (string.Equals(branch.Name, branchName, StringComparison.OrdinalIgnoreCase))
                {
                    targetBranch = branch;
                    break;
                }
            }

            if (targetBranch == null)
            {
                return new GitCommandResult(false, "The branch could not be found.", string.Empty);
            }

            if (targetBranch.IsCurrent)
            {
                return new GitCommandResult(false, "The current branch cannot be deleted.", string.Empty);
            }

            string[] arguments = new[] { "branch", "-D", branchName };
            return ExecuteResolvedRepositoryCommand(resolvedRepositoryPath, arguments, "Branch deleted successfully.", "Failed to delete the branch.");
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
                RepositoryCloneProgress alreadyProgress = new RepositoryCloneProgress(100.0, "Repository already cloned.", string.Empty);
                progress.Report(alreadyProgress);
                return new CloneRepositoryResult(true, true, repositoryPath, "Repository already cloned.");
            }

            RepositoryCloneProgress startProgress = new RepositoryCloneProgress(0.0, "Starting clone", string.Empty);
            progress.Report(startProgress);

            CommandResult result;
            string? personalAccessToken = null;

            try
            {
                personalAccessToken = await _secretProvider.GetSecretAsync("GitHubPersonalAccessToken", cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Unable to read the GitHub personal access token from the secret store.");
            }

            try
            {
                result = await ExecuteGitCloneAsync(_options.RootPath, repositoryUrl, repositoryPath, progress, personalAccessToken, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryDeleteDirectory(repositoryPath);
                string cancelledMessage = "Repository clone was canceled.";
                _logger.LogWarning("Git clone canceled for {RepositoryUrl}", repositoryUrl);
                return new CloneRepositoryResult(false, false, string.Empty, cancelledMessage, true);
            }

            if (result.Canceled)
            {
                TryDeleteDirectory(repositoryPath);
                string canceledMessage = "Repository clone was canceled.";
                _logger.LogWarning("Git clone canceled for {RepositoryUrl}", repositoryUrl);
                return new CloneRepositoryResult(false, false, string.Empty, canceledMessage, true);
            }

            if (!result.Succeeded)
            {
                TryDeleteDirectory(repositoryPath);
                string trimmedError = result.StandardError.Trim();
                string message = string.IsNullOrWhiteSpace(trimmedError) ? "Failed to clone repository." : trimmedError;
                _logger.LogError("Git clone failed for {RepositoryUrl}: {Message}", repositoryUrl, message);
                return new CloneRepositoryResult(false, false, string.Empty, message);
            }

            RepositoryCloneProgress completedProgress = new RepositoryCloneProgress(100.0, "Completed", string.Empty);
            progress.Report(completedProgress);
            return new CloneRepositoryResult(true, false, repositoryPath, string.Empty);
        }

        private static async Task<CommandResult> ExecuteGitCloneAsync(string workingDirectory, string repositoryUrl, string repositoryPath, IProgress<RepositoryCloneProgress> progress, string? personalAccessToken, CancellationToken cancellationToken)
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

            string effectiveRepositoryUrl = repositoryUrl;
            List<string> sensitiveValues = new List<string>();

            if (!string.IsNullOrWhiteSpace(personalAccessToken))
            {
                string authenticatedRepositoryUrl;
                IReadOnlyCollection<string> authenticationSensitiveValues;

                if (TryCreateAuthenticatedRepositoryUrl(repositoryUrl, personalAccessToken, out authenticatedRepositoryUrl, out authenticationSensitiveValues))
                {
                    effectiveRepositoryUrl = authenticatedRepositoryUrl;

                    foreach (string sensitiveValue in authenticationSensitiveValues)
                    {
                        if (!string.IsNullOrEmpty(sensitiveValue))
                        {
                            sensitiveValues.Add(sensitiveValue);
                        }
                    }
                }
            }

            startInfo.ArgumentList.Add(effectiveRepositoryUrl);
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
                        string sanitizedOutput = SanitizeSensitiveData(args.Data, sensitiveValues);
                        standardOutputBuilder.AppendLine(sanitizedOutput);
                    }
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        string trimmed = args.Data.Trim();
                        string sanitizedLine = SanitizeSensitiveData(args.Data, sensitiveValues);
                        standardErrorBuilder.AppendLine(sanitizedLine);

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

                                string sanitizedProgressMessage = SanitizeSensitiveData(trimmed, sensitiveValues);
                                RepositoryCloneProgress update = new RepositoryCloneProgress(lastPercentage, stage, sanitizedProgressMessage);
                                progress.Report(update);
                            }
                        }
                    }
                };

                TaskCompletionSource<bool> cancellationCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                using (cancellationToken.Register(() =>
                {
                    cancellationCompletionSource.TrySetResult(true);

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
                        string canceledOutput = SanitizeSensitiveData(standardOutputBuilder.ToString(), sensitiveValues);
                        string canceledError = SanitizeSensitiveData(standardErrorBuilder.ToString(), sensitiveValues);
                        return new CommandResult(false, canceledOutput, canceledError, true);
                    }

                    bool started = process.Start();

                    if (!started)
                    {
                        return new CommandResult(false, string.Empty, "Unable to start git process.");
                    }

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    Task waitForExitTask = process.WaitForExitAsync();
                    Task cancellationTask = cancellationCompletionSource.Task;
                    Task completedTask = await Task.WhenAny(waitForExitTask, cancellationTask).ConfigureAwait(false);

                    if (completedTask == cancellationTask)
                    {
                        try
                        {
                            await waitForExitTask.ConfigureAwait(false);
                        }
                        catch (InvalidOperationException)
                        {
                        }

                        string canceledOutput = SanitizeSensitiveData(standardOutputBuilder.ToString(), sensitiveValues);
                        string canceledError = SanitizeSensitiveData(standardErrorBuilder.ToString(), sensitiveValues);
                        return new CommandResult(false, canceledOutput, canceledError, true);
                    }

                    await waitForExitTask.ConfigureAwait(false);
                }

                string standardOutput = SanitizeSensitiveData(standardOutputBuilder.ToString(), sensitiveValues);
                string standardError = SanitizeSensitiveData(standardErrorBuilder.ToString(), sensitiveValues);
                bool success = process.ExitCode == 0;

                return new CommandResult(success, standardOutput, standardError);
            }
        }

        private static bool TryCreateAuthenticatedRepositoryUrl(string repositoryUrl, string personalAccessToken, out string authenticatedRepositoryUrl, out IReadOnlyCollection<string> sensitiveValues)
        {
            authenticatedRepositoryUrl = repositoryUrl;
            List<string> values = new List<string>();
            sensitiveValues = values;

            if (string.IsNullOrWhiteSpace(repositoryUrl))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(personalAccessToken))
            {
                return false;
            }

            Uri? repositoryUri;

            if (!Uri.TryCreate(repositoryUrl, UriKind.Absolute, out repositoryUri))
            {
                return false;
            }

            if (!string.Equals(repositoryUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            UriBuilder builder = new UriBuilder(repositoryUri);
            builder.UserName = "x-access-token";
            builder.Password = personalAccessToken;

            authenticatedRepositoryUrl = builder.Uri.AbsoluteUri;

            string rawUserInfo = string.Concat("x-access-token:", personalAccessToken);
            values.Add(personalAccessToken);
            values.Add(rawUserInfo);

            string escapedToken = Uri.EscapeDataString(personalAccessToken);

            if (!string.Equals(escapedToken, personalAccessToken, StringComparison.Ordinal))
            {
                string escapedUserInfo = string.Concat("x-access-token:", escapedToken);
                values.Add(escapedToken);
                values.Add(escapedUserInfo);
            }

            sensitiveValues = values;
            return true;
        }

        private static string SanitizeSensitiveData(string value, IReadOnlyCollection<string> sensitiveValues)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            if (sensitiveValues == null)
            {
                return value;
            }

            string sanitized = value;

            foreach (string sensitive in sensitiveValues)
            {
                if (string.IsNullOrEmpty(sensitive))
                {
                    continue;
                }

                sanitized = sanitized.Replace(sensitive, "***");
            }

            return sanitized;
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

        private static IReadOnlyCollection<RepositoryBranch> GetBranches(string repositoryPath)
        {
            List<RepositoryBranch> branches = new List<RepositoryBranch>();
            string[] arguments = new[] { "branch", "-vv" };
            CommandResult result = ExecuteGitCommand(repositoryPath, arguments);

            if (!result.Succeeded)
            {
                return branches;
            }

            string[] lines = result.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                bool isCurrent = line.StartsWith("*", StringComparison.Ordinal);
                string normalized = isCurrent ? line.Substring(1) : line;
                normalized = normalized.TrimStart();

                if (normalized.StartsWith("+", StringComparison.Ordinal))
                {
                    normalized = normalized.Substring(1).TrimStart();
                }

                Match match = Regex.Match(normalized, @"^(?<name>\S+)\s+(?<commit>\S+)(?:\s+\[(?<tracking>[^\]]+)\])?.*$");

                if (!match.Success)
                {
                    continue;
                }

                string name = match.Groups["name"].Value;
                string trackingContent = match.Groups["tracking"].Success ? match.Groups["tracking"].Value : string.Empty;
                bool hasUpstream = false;
                bool upstreamGone = false;
                string trackingBranch = string.Empty;
                int aheadCount = 0;
                int behindCount = 0;

                if (!string.IsNullOrWhiteSpace(trackingContent))
                {
                    hasUpstream = true;

                    if (string.Equals(trackingContent, "gone", StringComparison.OrdinalIgnoreCase))
                    {
                        upstreamGone = true;
                    }
                    else
                    {
                        int colonIndex = trackingContent.IndexOf(':');

                        if (colonIndex >= 0)
                        {
                            trackingBranch = trackingContent.Substring(0, colonIndex).Trim();
                            string statusSegment = trackingContent.Substring(colonIndex + 1).Trim();
                            string[] statusParts = statusSegment.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                            foreach (string statusPart in statusParts)
                            {
                                string trimmedPart = statusPart.Trim();

                                if (trimmedPart.StartsWith("ahead", StringComparison.OrdinalIgnoreCase))
                                {
                                    aheadCount = ParseTrackingCount(trimmedPart);
                                }
                                else if (trimmedPart.StartsWith("behind", StringComparison.OrdinalIgnoreCase))
                                {
                                    behindCount = ParseTrackingCount(trimmedPart);
                                }
                            }
                        }
                        else
                        {
                            trackingBranch = trackingContent.Trim();
                        }
                    }
                }

                RepositoryBranch branch = new RepositoryBranch(name, isCurrent, hasUpstream, upstreamGone, trackingBranch, aheadCount, behindCount);
                branches.Add(branch);
            }

            return branches
                .OrderBy(branch => branch.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static int ParseTrackingCount(string input)
        {
            string[] segments = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string segment in segments)
            {
                int parsedValue;

                if (int.TryParse(segment, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedValue))
                {
                    if (parsedValue < 0)
                    {
                        return 0;
                    }

                    return parsedValue;
                }
            }

            return 0;
        }

        private static string GetRemoteUrl(string repositoryPath)
        {
            string[] arguments = new[] { "config", "--get", "remote.origin.url" };
            CommandResult result = ExecuteGitCommand(repositoryPath, arguments);

            if (!result.Succeeded)
            {
                return string.Empty;
            }

            string remoteUrl = string.IsNullOrWhiteSpace(result.StandardOutput) ? string.Empty : result.StandardOutput.Trim();

            if (remoteUrl.Length == 0)
            {
                return string.Empty;
            }

            return RemoveCredentialsFromUrl(remoteUrl);
        }

        private static string RemoveCredentialsFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            string trimmed = url.Trim();
            int schemeSeparatorIndex = trimmed.IndexOf("://", StringComparison.Ordinal);

            if (schemeSeparatorIndex < 0)
            {
                return trimmed;
            }

            int credentialsEndIndex = trimmed.IndexOf('@', schemeSeparatorIndex + 3);

            if (credentialsEndIndex < 0)
            {
                return trimmed;
            }

            string prefix = trimmed.Substring(0, schemeSeparatorIndex + 3);
            string remainder = trimmed.Substring(credentialsEndIndex + 1);
            return string.Concat(prefix, remainder);
        }

        private static DateTimeOffset? GetLastFetchTimeUtc(string repositoryPath)
        {
            try
            {
                string gitDirectoryPath = Path.Combine(repositoryPath, ".git");
                string fetchHeadPath = Path.Combine(gitDirectoryPath, "FETCH_HEAD");

                if (!File.Exists(fetchHeadPath))
                {
                    return null;
                }

                DateTime fetchHeadWriteTimeUtc = File.GetLastWriteTimeUtc(fetchHeadPath);

                if (fetchHeadWriteTimeUtc == DateTime.MinValue || fetchHeadWriteTimeUtc == DateTime.MaxValue)
                {
                    return null;
                }

                DateTimeOffset fetchHeadTimestamp = new DateTimeOffset(fetchHeadWriteTimeUtc, TimeSpan.Zero);
                return fetchHeadTimestamp;
            }
            catch (Exception)
            {
                return null;
            }
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

        public RemoteBranchQueryResult GetRemoteBranches(string repositoryPath, string searchTerm)
        {
            string resolvedRepositoryPath;
            string validationMessage;

            if (!TryResolveRepositoryPath(repositoryPath, out resolvedRepositoryPath, out validationMessage))
            {
                List<RepositoryRemoteBranch> emptyBranches = new List<RepositoryRemoteBranch>();
                return new RemoteBranchQueryResult(false, validationMessage, emptyBranches);
            }

            IReadOnlyCollection<RepositoryBranch> existingBranches = GetBranches(resolvedRepositoryPath);
            HashSet<string> localBranchNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (RepositoryBranch branch in existingBranches)
            {
                if (!localBranchNames.Contains(branch.Name))
                {
                    localBranchNames.Add(branch.Name);
                }
            }

            string[] arguments = new[] { "branch", "-r" };
            CommandResult result = ExecuteGitCommand(resolvedRepositoryPath, arguments, TimeSpan.FromMinutes(1));

            if (!result.Succeeded)
            {
                string combinedOutput = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
                string trimmedError = string.IsNullOrWhiteSpace(combinedOutput) ? string.Empty : combinedOutput.Trim();
                string errorMessage = string.IsNullOrWhiteSpace(trimmedError) ? "Failed to retrieve remote branches." : trimmedError;
                string argumentsDisplay = string.Join(" ", arguments);
                _logger.LogWarning("Git command {Arguments} failed for {RepositoryPath}: {Message}", argumentsDisplay, resolvedRepositoryPath, errorMessage);
                List<RepositoryRemoteBranch> emptyBranches = new List<RepositoryRemoteBranch>();
                return new RemoteBranchQueryResult(false, errorMessage, emptyBranches);
            }

            string output = result.StandardOutput ?? string.Empty;
            string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            List<RepositoryRemoteBranch> branches = new List<RepositoryRemoteBranch>();
            string filter = searchTerm ?? string.Empty;

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();

                if (string.IsNullOrWhiteSpace(trimmedLine))
                {
                    continue;
                }

                if (trimmedLine.IndexOf("->", StringComparison.Ordinal) >= 0)
                {
                    continue;
                }

                int separatorIndex = trimmedLine.IndexOf('/');

                if (separatorIndex <= 0 || separatorIndex == trimmedLine.Length - 1)
                {
                    continue;
                }

                string remoteName = trimmedLine.Substring(0, separatorIndex);

                if (!string.Equals(remoteName, "origin", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string branchSegment = trimmedLine.Substring(separatorIndex + 1).Trim();

                if (string.IsNullOrWhiteSpace(branchSegment))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(filter))
                {
                    if (branchSegment.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }
                }

                bool existsLocally = localBranchNames.Contains(branchSegment);
                RepositoryRemoteBranch remoteBranch = new RepositoryRemoteBranch(remoteName, branchSegment, existsLocally);
                branches.Add(remoteBranch);
            }

            List<RepositoryRemoteBranch> orderedBranches = branches
                .OrderBy(branch => branch.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new RemoteBranchQueryResult(true, "Remote branches loaded successfully.", orderedBranches);
        }

        private GitCommandResult ExecuteRepositoryCommand(string repositoryPath, IReadOnlyCollection<string> arguments, string successFallbackMessage, string errorFallbackMessage)
        {
            string resolvedRepositoryPath;
            string validationMessage;

            if (!TryResolveRepositoryPath(repositoryPath, out resolvedRepositoryPath, out validationMessage))
            {
                return new GitCommandResult(false, validationMessage, string.Empty);
            }

            return ExecuteResolvedRepositoryCommand(resolvedRepositoryPath, arguments, successFallbackMessage, errorFallbackMessage);
        }

        private GitCommandResult ExecuteResolvedRepositoryCommand(string resolvedRepositoryPath, IReadOnlyCollection<string> arguments, string successFallbackMessage, string errorFallbackMessage)
        {
            CommandResult result = ExecuteGitCommand(resolvedRepositoryPath, arguments, TimeSpan.FromMinutes(5));

            if (!result.Succeeded)
            {
                string combinedOutput = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
                string trimmedError = string.IsNullOrWhiteSpace(combinedOutput) ? string.Empty : combinedOutput.Trim();
                string errorMessage = string.IsNullOrWhiteSpace(trimmedError) ? errorFallbackMessage : trimmedError;
                string argumentsDisplay = string.Join(" ", arguments);
                _logger.LogWarning("Git command {Arguments} failed for {RepositoryPath}: {Message}", argumentsDisplay, resolvedRepositoryPath, errorMessage);
                return new GitCommandResult(false, errorMessage, trimmedError);
            }

            string trimmedOutput = string.IsNullOrWhiteSpace(result.StandardOutput) ? string.Empty : result.StandardOutput.Trim();
            string successMessage = string.IsNullOrWhiteSpace(trimmedOutput) ? successFallbackMessage : trimmedOutput;
            return new GitCommandResult(true, successMessage, trimmedOutput);
        }

        private bool TryResolveRepositoryPath(string repositoryPath, out string resolvedRepositoryPath, out string errorMessage)
        {
            resolvedRepositoryPath = string.Empty;
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(repositoryPath))
            {
                errorMessage = "The repository path must be provided.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_options.RootPath))
            {
                string message = "Repository root path is not configured.";
                _logger.LogWarning(message);
                errorMessage = message;
                return false;
            }

            string fullRootPath;
            string candidateRepositoryPath;

            try
            {
                fullRootPath = Path.GetFullPath(_options.RootPath);
                candidateRepositoryPath = repositoryPath;

                if (!Path.IsPathRooted(candidateRepositoryPath))
                {
                    candidateRepositoryPath = Path.Combine(fullRootPath, candidateRepositoryPath);
                }

                candidateRepositoryPath = Path.GetFullPath(candidateRepositoryPath);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException || exception is SecurityException)
            {
                _logger.LogWarning(exception, "Invalid repository path provided: {RepositoryPath}", repositoryPath);
                errorMessage = "The repository path is invalid.";
                return false;
            }

            if (!candidateRepositoryPath.StartsWith(fullRootPath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Repository path {RepositoryPath} is outside of the configured storage root.", candidateRepositoryPath);
                errorMessage = "The repository path is invalid.";
                return false;
            }

            if (!Directory.Exists(candidateRepositoryPath))
            {
                errorMessage = "The repository could not be found.";
                return false;
            }

            string gitDirectoryPath = Path.Combine(candidateRepositoryPath, ".git");

            if (!Directory.Exists(gitDirectoryPath))
            {
                errorMessage = "The repository could not be found.";
                return false;
            }

            resolvedRepositoryPath = candidateRepositoryPath;
            return true;
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
            public CommandResult(bool succeeded, string standardOutput, string standardError)
                : this(succeeded, standardOutput, standardError, false)
            {
            }

            public CommandResult(bool succeeded, string standardOutput, string standardError, bool canceled)
            {
                Succeeded = succeeded;
                StandardOutput = standardOutput;
                StandardError = standardError;
                Canceled = canceled;
            }

            public bool Succeeded { get; }

            public string StandardOutput { get; }

            public string StandardError { get; }

            public bool Canceled { get; }
        }

        private readonly struct RepositorySyncInfo
        {
            public RepositorySyncInfo(bool hasUncommittedChanges, bool hasUnpushedCommits)
            {
                HasUncommittedChanges = hasUncommittedChanges;
                HasUnpushedCommits = hasUnpushedCommits;
            }

            public bool HasUncommittedChanges { get; }

            public bool HasUnpushedCommits { get; }
        }

        private static RepositorySyncInfo GetRepositorySyncInfo(string repositoryPath)
        {
            try
            {
                string[] arguments = new[] { "status", "--porcelain=1", "--branch" };
                CommandResult result = ExecuteGitCommand(repositoryPath, arguments, TimeSpan.FromSeconds(10));

                if (!result.Succeeded)
                {
                    return new RepositorySyncInfo(false, false);
                }

                bool hasUncommittedChanges = false;
                bool hasUnpushedCommits = false;
                string[] lines = result.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                for (int index = 0; index < lines.Length; index++)
                {
                    string line = lines[index];

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    if (index == 0 && line.StartsWith("##", StringComparison.Ordinal))
                    {
                        string normalized = line.ToLowerInvariant();

                        if (normalized.Contains("ahead"))
                        {
                            hasUnpushedCommits = true;
                        }

                        continue;
                    }

                    hasUncommittedChanges = true;
                    break;
                }

                return new RepositorySyncInfo(hasUncommittedChanges, hasUnpushedCommits);
            }
            catch (Exception)
            {
                return new RepositorySyncInfo(false, false);
            }
        }
    }
}
