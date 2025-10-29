using System;

namespace MyApp.Models.Home
{
    public sealed class RepositoryBranchViewModel
    {
        public RepositoryBranchViewModel(string name, bool isCurrent, bool hasUpstream, bool upstreamGone, string trackingBranch, int aheadCount, int behindCount)
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

        public string TrackingSummary
        {
            get
            {
                if (!HasUpstream)
                {
                    return "No upstream configured.";
                }

                if (UpstreamGone)
                {
                    return "Upstream branch is gone.";
                }

                string prefix = string.IsNullOrWhiteSpace(TrackingBranch) ? "Tracking remote branch" : string.Format("Tracking {0}", TrackingBranch);
                string aheadText = AheadCount > 0 ? FormatCount("ahead", AheadCount) : string.Empty;
                string behindText = BehindCount > 0 ? FormatCount("behind", BehindCount) : string.Empty;

                if (string.IsNullOrEmpty(aheadText) && string.IsNullOrEmpty(behindText))
                {
                    return string.Format("{0} — synchronized.", prefix);
                }

                if (!string.IsNullOrEmpty(aheadText) && !string.IsNullOrEmpty(behindText))
                {
                    return string.Format("{0} — {1}, {2}.", prefix, aheadText, behindText);
                }

                string status = !string.IsNullOrEmpty(aheadText) ? aheadText : behindText;
                return string.Format("{0} — {1}.", prefix, status);
            }
        }

        private static string FormatCount(string descriptor, int count)
        {
            string pluralSuffix = count == 1 ? string.Empty : "s";
            return string.Format("{0} by {1} commit{2}", Capitalize(descriptor), count, pluralSuffix);
        }

        private static string Capitalize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.Length == 1)
            {
                return value.ToUpperInvariant();
            }

            string firstCharacter = value.Substring(0, 1).ToUpperInvariant();
            string remainder = value.Substring(1);
            return string.Concat(firstCharacter, remainder);
        }
    }
}

