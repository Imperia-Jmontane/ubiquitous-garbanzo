using System;
using System.Collections.Generic;

namespace MyApp.Domain.Repositories
{
    public sealed class LocalRepository
    {
        public LocalRepository(string name, string fullPath, string remoteUrl, IReadOnlyCollection<string> branches, bool hasUncommittedChanges, bool hasUnpushedCommits)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("The repository name must be provided.", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(fullPath))
            {
                throw new ArgumentException("The repository path must be provided.", nameof(fullPath));
            }

            if (branches == null)
            {
                throw new ArgumentNullException(nameof(branches));
            }

            Name = name;
            FullPath = fullPath;
            RemoteUrl = remoteUrl ?? string.Empty;
            Branches = branches;
            HasUncommittedChanges = hasUncommittedChanges;
            HasUnpushedCommits = hasUnpushedCommits;
        }

        public string Name { get; }

        public string FullPath { get; }

        public string RemoteUrl { get; }

        public IReadOnlyCollection<string> Branches { get; }

        public bool HasUncommittedChanges { get; }

        public bool HasUnpushedCommits { get; }

        public bool HasRemote
        {
            get
            {
                return !string.IsNullOrWhiteSpace(RemoteUrl);
            }
        }
    }
}
