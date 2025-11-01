namespace MyApp.Models.Profile
{
    public sealed class FlowBranchPreferenceResponse
    {
        public bool Succeeded { get; set; }

        public string Message { get; set; } = string.Empty;

        public bool CreateLinkedBranches { get; set; }
    }
}
