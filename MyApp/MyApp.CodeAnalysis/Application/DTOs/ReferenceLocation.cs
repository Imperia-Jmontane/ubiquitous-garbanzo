namespace MyApp.CodeAnalysis.Application.DTOs
{
    public sealed class ReferenceLocation
    {
        public string FilePath { get; set; } = string.Empty;

        public int Line { get; set; }

        public int Column { get; set; }

        public int EndLine { get; set; }

        public int EndColumn { get; set; }

        public string? Context { get; set; }
    }
}
