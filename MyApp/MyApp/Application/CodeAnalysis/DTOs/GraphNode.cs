namespace MyApp.Application.CodeAnalysis.DTOs
{
    public sealed class GraphNode
    {
        public long Id { get; set; }

        public string SerializedName { get; set; } = string.Empty;

        public string? DisplayName { get; set; }

        public string Type { get; set; } = string.Empty;

        public string? FilePath { get; set; }

        public int? Line { get; set; }

        public int? Column { get; set; }

        public long? ParentId { get; set; }
    }
}
