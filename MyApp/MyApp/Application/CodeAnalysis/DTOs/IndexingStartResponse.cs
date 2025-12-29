namespace MyApp.Application.CodeAnalysis.DTOs
{
    public sealed class IndexingStartResponse
    {
        public long JobId { get; set; }

        public string Status { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }
}
