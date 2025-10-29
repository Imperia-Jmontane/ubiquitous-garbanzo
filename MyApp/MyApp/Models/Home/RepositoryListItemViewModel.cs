using System;
using System.Collections.Generic;
using System.Linq;
using MyApp.Domain.Repositories;

namespace MyApp.Models.Home
{
    public sealed class RepositoryListItemViewModel
    {
        public RepositoryListItemViewModel(string name, string repositoryPath, string remoteUrl, IReadOnlyCollection<RepositoryBranch> branches, bool hasUncommittedChanges, bool hasUnpushedCommits, DateTimeOffset? lastFetchTimeUtc)
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
            Branches = CreateBranchViewModels(branches);
            HasUncommittedChanges = hasUncommittedChanges;
            HasUnpushedCommits = hasUnpushedCommits;
            LastFetchTimeUtc = lastFetchTimeUtc;
        }

        public string Name { get; }

        public string RepositoryPath { get; }

        public string RemoteUrl { get; }

        public IReadOnlyCollection<RepositoryBranchViewModel> Branches { get; }

        public bool HasUncommittedChanges { get; }

        public bool HasUnpushedCommits { get; }

        public DateTimeOffset? LastFetchTimeUtc { get; }

        public bool HasBranches
        {
            get
            {
                return Branches.Count > 0;
            }
        }

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

        public string FetchStatusLabel
        {
            get
            {
                if (!LastFetchTimeUtc.HasValue)
                {
                    return "Fetch has not been executed yet.";
                }

                DateTimeOffset now = DateTimeOffset.UtcNow;
                TimeSpan elapsed = now - LastFetchTimeUtc.Value;

                if (elapsed < TimeSpan.Zero)
                {
                    elapsed = TimeSpan.Zero;
                }

                return string.Format("Last fetch {0}.", FormatElapsed(elapsed));
            }
        }

        private static IReadOnlyCollection<RepositoryBranchViewModel> CreateBranchViewModels(IReadOnlyCollection<RepositoryBranch> branches)
        {
            List<RepositoryBranchViewModel> branchViewModels = new List<RepositoryBranchViewModel>();

            foreach (RepositoryBranch branch in branches)
            {
                RepositoryBranchViewModel branchViewModel = new RepositoryBranchViewModel(branch.Name, branch.IsCurrent, branch.HasUpstream, branch.UpstreamGone, branch.TrackingBranch, branch.AheadCount, branch.BehindCount);
                branchViewModels.Add(branchViewModel);
            }

            return branchViewModels
                .OrderBy(branch => branch.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string FormatElapsed(TimeSpan elapsed)
        {
            if (elapsed.TotalSeconds < 60)
            {
                return "less than a minute ago";
            }

            if (elapsed.TotalMinutes < 60)
            {
                int minutes = (int)Math.Floor(elapsed.TotalMinutes);

                if (minutes <= 1)
                {
                    return "1 minute ago";
                }

                return string.Format("{0} minutes ago", minutes);
            }

            if (elapsed.TotalHours < 24)
            {
                int hours = (int)Math.Floor(elapsed.TotalHours);

                if (hours <= 1)
                {
                    return "1 hour ago";
                }

                return string.Format("{0} hours ago", hours);
            }

            int days = (int)Math.Floor(elapsed.TotalDays);

            if (days <= 1)
            {
                return "1 day ago";
            }

            return string.Format("{0} days ago", days);
        }
    }
}

