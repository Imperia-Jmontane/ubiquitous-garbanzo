using System;
using Microsoft.AspNetCore.Mvc;
using MyApp.Application.Abstractions;

namespace MyApp.Controllers.Api
{
    [ApiController]
    [Route("api/repositories")]
    public sealed class RepositoriesController : ControllerBase
    {
        private readonly ILocalRepositoryService _repositoryService;

        public RepositoriesController(ILocalRepositoryService repositoryService)
        {
            if (repositoryService == null)
            {
                throw new ArgumentNullException(nameof(repositoryService));
            }

            _repositoryService = repositoryService;
        }

        [HttpDelete]
        public ActionResult<DeleteRepositoryResponse> DeleteRepository([FromBody] DeleteRepositoryRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.RepositoryPath))
            {
                DeleteRepositoryResponse errorResponse = new DeleteRepositoryResponse
                {
                    Succeeded = false,
                    Message = "The repository path must be provided."
                };

                return BadRequest(errorResponse);
            }

            DeleteRepositoryResult result = _repositoryService.DeleteRepository(request.RepositoryPath);

            if (!result.Succeeded)
            {
                DeleteRepositoryResponse errorResponse = new DeleteRepositoryResponse
                {
                    Succeeded = false,
                    Message = string.IsNullOrWhiteSpace(result.Message) ? "Unable to delete the repository." : result.Message
                };

                return BadRequest(errorResponse);
            }

            DeleteRepositoryResponse response = new DeleteRepositoryResponse
            {
                Succeeded = true,
                Message = "Repository deleted."
            };

            return Ok(response);
        }

        public sealed class DeleteRepositoryRequest
        {
            public string RepositoryPath { get; set; } = string.Empty;
        }

        public sealed class DeleteRepositoryResponse
        {
            public bool Succeeded { get; set; }

            public string Message { get; set; } = string.Empty;
        }
    }
}
