namespace MyApp.CodeAnalysis.Domain.CodeAnalysis
{
    public sealed class Occurrence
    {
        public long ElementId { get; set; }

        public long SourceLocationId { get; set; }

        public SourceLocation SourceLocation { get; set; } = null!;
    }
}
