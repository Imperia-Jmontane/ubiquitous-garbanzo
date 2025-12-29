using System.ComponentModel.DataAnnotations;

namespace MyApp.Models.CodeAnalysis
{
    public sealed class CodeAnalysisIndexRequest
    {
        [Required]
        public string RepositoryId { get; set; } = string.Empty;
    }
}
