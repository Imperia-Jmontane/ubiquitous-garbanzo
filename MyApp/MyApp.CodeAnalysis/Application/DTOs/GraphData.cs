using System.Collections.Generic;

namespace MyApp.CodeAnalysis.Application.DTOs
{
    public sealed class GraphData
    {
        public List<GraphNode> Nodes { get; set; } = new List<GraphNode>();

        public List<GraphEdge> Edges { get; set; } = new List<GraphEdge>();

        public bool HasMore { get; set; }
    }
}
