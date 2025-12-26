namespace MyApp.Application.CodeAnalysis.DTOs
{
    public sealed class SymbolSearchResult
    {
        public long Id { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public string SerializedName { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public string? FilePath { get; set; }

        public int? Line { get; set; }
    }
}
