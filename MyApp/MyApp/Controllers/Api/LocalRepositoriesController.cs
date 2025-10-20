using System;
using Microsoft.AspNetCore.Mvc;
using MyApp.Application.Abstractions;

namespace MyApp.Controllers.Api
{
    [ApiController]
    [Route("api/repositories")]
    public sealed class LocalRepositoriesController : ControllerBase
    {
        private readonly ILocalRepositoryService _repositoryService;

        public LocalRepositoriesController(ILocalRepositoryService repositoryService)
        {
            if (repositoryService == null)
            {
                throw new ArgumentNullException(nameof(repositoryService));
            }

            _repositoryService = repositoryService;
        }

        [HttpDelete("{repositoryName}")]
        public ActionResult<DeleteRepositoryResponse> DeleteRepository(string repositoryName)
        {
            if (string.IsNullOrWhiteSpace(repositoryName))
            {
                DeleteRepositoryResponse errorResponse = new DeleteRepositoryResponse
                {
                    Message = "The repository name must be provided.",
                    Deleted = false
                };
                return BadRequest(errorResponse);
            }

            DeleteRepositoryResult result = _repositoryService.DeleteRepository(repositoryName);

            if (result.NotFound)
            {
                DeleteRepositoryResponse notFoundResponse = new DeleteRepositoryResponse
                {
                    Message = "The repository was not found.",
                    Deleted = false
                };
                return NotFound(notFoundResponse);
            }

            if (!result.Succeeded)
            {
                DeleteRepositoryResponse failureResponse = new DeleteRepositoryResponse
                {
                    Message = string.IsNullOrWhiteSpace(result.Message) ? "Failed to delete the repository." : result.Message,
                    Deleted = false
                };
                return StatusCode(500, failureResponse);
            }

            DeleteRepositoryResponse successResponse = new DeleteRepositoryResponse
            {
                Message = string.IsNullOrWhiteSpace(result.Message) ? "Repository deleted." : result.Message,
                Deleted = true
            };

            return Ok(successResponse);
        }

        public sealed class DeleteRepositoryResponse
        {
            public bool Deleted { get; set; }

            public string Message { get; set; } = string.Empty;
        }
    }
}
