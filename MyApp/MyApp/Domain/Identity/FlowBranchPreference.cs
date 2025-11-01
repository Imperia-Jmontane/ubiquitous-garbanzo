using System;

namespace MyApp.Domain.Identity
{
    public sealed class FlowBranchPreference
    {
        public FlowBranchPreference(Guid userId, bool createLinkedBranches)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("The user identifier cannot be empty.", nameof(userId));
            }

            UserId = userId;
            CreateLinkedBranches = createLinkedBranches;
            UpdatedAt = DateTimeOffset.UtcNow;
        }

        private FlowBranchPreference()
        {
        }

        public Guid UserId { get; private set; }

        public bool CreateLinkedBranches { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public void SetCreateLinkedBranches(bool createLinkedBranches)
        {
            CreateLinkedBranches = createLinkedBranches;
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
