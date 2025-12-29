namespace MyApp.Application.Configuration
{
    public sealed class CodeAnalysisOptions
    {
        public int MaxIndexingConcurrency { get; set; } = 1;

        public int IndexingTimeoutMinutes { get; set; } = 30;
    }
}
