using System;

namespace MyApp.Domain.Repositories
{
    public sealed class RepositoryBranch
    {
        public RepositoryBranch(string name, bool isCurrent, bool hasUpstream, bool upstreamGone, string trackingBranch, int aheadCount, int behindCount)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("The branch name must be provided.", nameof(name));
            }

            Name = name;
            IsCurrent = isCurrent;
            HasUpstream = hasUpstream;
            UpstreamGone = upstreamGone;
            TrackingBranch = trackingBranch ?? string.Empty;
            AheadCount = aheadCount < 0 ? 0 : aheadCount;
            BehindCount = behindCount < 0 ? 0 : behindCount;
        }

        public string Name { get; }

        public bool IsCurrent { get; }

        public bool HasUpstream { get; }

        public bool UpstreamGone { get; }

        public string TrackingBranch { get; }

        public int AheadCount { get; }

        public int BehindCount { get; }

        public bool IsSynchronized
        {
            get
            {
                if (!HasUpstream || UpstreamGone)
                {
                    return false;
                }

                return AheadCount == 0 && BehindCount == 0;
            }
        }
    }
}

