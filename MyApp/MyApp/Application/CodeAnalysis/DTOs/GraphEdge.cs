namespace MyApp.Application.CodeAnalysis.DTOs
{
    public sealed class GraphEdge
    {
        public long Id { get; set; }

        public long SourceNodeId { get; set; }

        public long TargetNodeId { get; set; }

        public string Type { get; set; } = string.Empty;
    }
}
