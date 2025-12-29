using System.Collections.Generic;

namespace MyApp.Application.CodeAnalysis.DTOs
{
    public sealed class InheritanceResponse
    {
        public List<SymbolSearchResult> Ancestors { get; set; } = new List<SymbolSearchResult>();

        public List<SymbolSearchResult> Descendants { get; set; } = new List<SymbolSearchResult>();
    }
}
