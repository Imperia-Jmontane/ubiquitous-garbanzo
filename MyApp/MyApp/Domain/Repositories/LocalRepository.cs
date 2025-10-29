using System;
using System.Collections.Generic;

namespace MyApp.Domain.Repositories
{
    public sealed class LocalRepository
    {
        public LocalRepository(string name, string fullPath, string remoteUrl, IReadOnlyCollection<RepositoryBranch> branches, bool hasUncommittedChanges, bool hasUnpushedCommits, DateTimeOffset? lastFetchTimeUtc)
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
            LastFetchTimeUtc = lastFetchTimeUtc;
        }

        public string Name { get; }

        public string FullPath { get; }

        public string RemoteUrl { get; }

        public IReadOnlyCollection<RepositoryBranch> Branches { get; }

        public bool HasUncommittedChanges { get; }

        public bool HasUnpushedCommits { get; }

        public DateTimeOffset? LastFetchTimeUtc { get; }

        public bool HasUnsyncedChanges
        {
            get
            {
                return HasUncommittedChanges || HasUnpushedCommits;
            }
        }

        public bool HasRemote
        {
            get
            {
                return !string.IsNullOrWhiteSpace(RemoteUrl);
            }
        }

        public bool HasBranches
        {
            get
            {
                return Branches.Count > 0;
            }
        }
    }
}

