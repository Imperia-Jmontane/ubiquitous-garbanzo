using System;
using System.Collections.Generic;
using System.Linq;

namespace MyApp.Models.Home
{
    public sealed class RepositoryListItemViewModel
    {
        public RepositoryListItemViewModel(string name, string repositoryPath, string remoteUrl, IReadOnlyCollection<string> branches, bool hasUncommittedChanges, bool hasUnpushedCommits)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("The repository name must be provided.", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(repositoryPath))
            {
                throw new ArgumentException("The repository path must be provided.", nameof(repositoryPath));
            }

            if (branches == null)
            {
                throw new ArgumentNullException(nameof(branches));
            }

            Name = name;
            RepositoryPath = repositoryPath;
            RemoteUrl = remoteUrl ?? string.Empty;
            Branches = branches.ToList();
            HasUncommittedChanges = hasUncommittedChanges;
            HasUnpushedCommits = hasUnpushedCommits;
        }

        public string Name { get; }

        public string RepositoryPath { get; }

        public string RemoteUrl { get; }

        public IReadOnlyCollection<string> Branches { get; }

        public bool HasUncommittedChanges { get; }

        public bool HasUnpushedCommits { get; }

        public bool HasBranches
        {
            get
            {
                return Branches.Count > 0;
            }
        }

        public bool HasRemote
        {
            get
            {
                return !string.IsNullOrWhiteSpace(RemoteUrl);
            }
        }

        public bool HasUnsyncedChanges
        {
            get
            {
                return HasUncommittedChanges || HasUnpushedCommits;
            }
        }
    }
}
