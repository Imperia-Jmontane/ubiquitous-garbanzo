using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        private static CommandResult ExecuteGitCommand(string repositoryPath, IReadOnlyCollection<string> arguments)
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
                    return new CommandResult(false, string.Empty, string.Empty);
                }

                string standardOutput = process.StandardOutput.ReadToEnd();
                string standardError = process.StandardError.ReadToEnd();
                process.WaitForExit();

                bool success = process.ExitCode == 0;

                return new CommandResult(success, standardOutput, standardError);
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
