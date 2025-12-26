namespace MyApp.CodeAnalysis.Domain.CodeAnalysis
{
    public enum IndexingStatus
    {
        Queued = 0,
        Running = 1,
        Completed = 2,
        Failed = 3,
        Cancelled = 4
    }
}
