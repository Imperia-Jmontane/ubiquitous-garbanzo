using System.ComponentModel.DataAnnotations;

namespace MyApp.Models.Home
{
    public sealed class AddRepositoryRequest
    {
        public AddRepositoryRequest()
        {
            RepositoryUrl = string.Empty;
        }

        [Required]
        [Display(Name = "Repository HTTPS URL")]
        [DataType(DataType.Url)]
        public string RepositoryUrl { get; set; }
    }
}
