namespace MyApp.Application.Profile.DTOs
{
    public sealed class FlowBranchPreferenceDto
    {
        public FlowBranchPreferenceDto(bool createLinkedBranches)
        {
            CreateLinkedBranches = createLinkedBranches;
        }

        public bool CreateLinkedBranches { get; }
    }
}
