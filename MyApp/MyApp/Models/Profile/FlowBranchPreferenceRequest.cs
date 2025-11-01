using System;

namespace MyApp.Models.Profile
{
    public sealed class FlowBranchPreferenceRequest
    {
        public Guid UserId { get; set; }

        public bool CreateLinkedBranches { get; set; }
    }
}
