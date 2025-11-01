using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using MyApp.Application.Abstractions;
using MyApp.Domain.Repositories;

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

        [HttpPost("fetch")]
        public ActionResult<RepositoryCommandResponse> FetchRepository([FromBody] RepositoryCommandRequest request)
        {
            return ExecuteRepositoryCommand(request, _repositoryService.FetchRepository);
        }

        [HttpPost("pull")]
        public ActionResult<RepositoryCommandResponse> PullRepository([FromBody] RepositoryCommandRequest request)
        {
            return ExecuteRepositoryCommand(request, _repositoryService.PullRepository);
        }

        [HttpPost("push")]
        public ActionResult<RepositoryCommandResponse> PushRepository([FromBody] RepositoryCommandRequest request)
        {
            return ExecuteRepositoryCommand(request, _repositoryService.PushRepository);
        }

        [HttpPost("commit")]
        public ActionResult<RepositoryCommandResponse> CommitRepository([FromBody] RepositoryCommandRequest request)
        {
            return ExecuteRepositoryCommand(request, _repositoryService.CommitRepository);
        }

        [HttpPost("publish-branch")]
        public ActionResult<RepositoryCommandResponse> PublishBranch([FromBody] PublishBranchRequest request)
        {
            string repositoryPath = request == null ? string.Empty : request.RepositoryPath;
            string branchName = request == null ? string.Empty : request.BranchName;
            return ExecuteRepositoryBranchCommand(repositoryPath, branchName, _repositoryService.PublishBranch);
        }

        [HttpPost("switch-branch")]
        public ActionResult<RepositoryCommandResponse> SwitchBranch([FromBody] SwitchBranchRequest request)
        {
            string repositoryPath = request == null ? string.Empty : request.RepositoryPath;
            string branchName = request == null ? string.Empty : request.BranchName;
            bool useLinkedFlowBranch = request != null && request.UseLinkedFlowBranch;

            if (string.IsNullOrWhiteSpace(repositoryPath) || string.IsNullOrWhiteSpace(branchName))
            {
                RepositoryCommandResponse errorResponse = new RepositoryCommandResponse
                {
                    Succeeded = false,
                    Message = "The repository path and branch name must be provided.",
                    Output = string.Empty
                };

                return BadRequest(errorResponse);
            }

            GitCommandResult result = _repositoryService.SwitchBranch(repositoryPath, branchName, useLinkedFlowBranch);

            if (!result.Succeeded)
            {
                RepositoryCommandResponse errorResponse = new RepositoryCommandResponse
                {
                    Succeeded = false,
                    Message = string.IsNullOrWhiteSpace(result.Message) ? "The operation could not be completed." : result.Message,
                    Output = result.Output
                };

                return BadRequest(errorResponse);
            }

            RepositoryCommandResponse response = new RepositoryCommandResponse
            {
                Succeeded = true,
                Message = string.IsNullOrWhiteSpace(result.Message) ? "Operation completed successfully." : result.Message,
                Output = result.Output
            };

            return Ok(response);
        }

        [HttpDelete("branches")]
        public ActionResult<RepositoryCommandResponse> DeleteBranch([FromBody] DeleteBranchRequest request)
        {
            string repositoryPath = request == null ? string.Empty : request.RepositoryPath;
            string branchName = request == null ? string.Empty : request.BranchName;
            return ExecuteRepositoryBranchCommand(repositoryPath, branchName, _repositoryService.DeleteBranch);
        }

        [HttpGet("remote-branches")]
        public ActionResult<RemoteBranchesResponse> GetRemoteBranches([FromQuery(Name = "repositoryPath")] string repositoryPath, [FromQuery(Name = "query")] string? query)
        {
            string path = repositoryPath ?? string.Empty;
            string search = query ?? string.Empty;
            RemoteBranchQueryResult result = _repositoryService.GetRemoteBranches(path, search);

            if (!result.Succeeded)
            {
                RemoteBranchesResponse errorResponse = new RemoteBranchesResponse
                {
                    Succeeded = false,
                    Message = string.IsNullOrWhiteSpace(result.Message) ? "Failed to load remote branches." : result.Message,
                    Branches = new List<RemoteBranchResponseItem>()
                };

                return BadRequest(errorResponse);
            }

            List<RemoteBranchResponseItem> branches = new List<RemoteBranchResponseItem>();

            foreach (RepositoryRemoteBranch branch in result.Branches)
            {
                RemoteBranchResponseItem responseBranch = new RemoteBranchResponseItem
                {
                    Name = branch.Name,
                    RemoteName = branch.RemoteName,
                    ExistsLocally = branch.ExistsLocally
                };

                branches.Add(responseBranch);
            }

            RemoteBranchesResponse response = new RemoteBranchesResponse
            {
                Succeeded = true,
                Message = result.Message,
                Branches = branches
            };

            return Ok(response);
        }

        private ActionResult<RepositoryCommandResponse> ExecuteRepositoryCommand(RepositoryCommandRequest request, Func<string, GitCommandResult> executor)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.RepositoryPath))
            {
                RepositoryCommandResponse errorResponse = new RepositoryCommandResponse
                {
                    Succeeded = false,
                    Message = "The repository path must be provided.",
                    Output = string.Empty
                };

                return BadRequest(errorResponse);
            }

            GitCommandResult result = executor(request.RepositoryPath);

            if (!result.Succeeded)
            {
                RepositoryCommandResponse errorResponse = new RepositoryCommandResponse
                {
                    Succeeded = false,
                    Message = string.IsNullOrWhiteSpace(result.Message) ? "The operation could not be completed." : result.Message,
                    Output = result.Output
                };

                return BadRequest(errorResponse);
            }

            RepositoryCommandResponse response = new RepositoryCommandResponse
            {
                Succeeded = true,
                Message = string.IsNullOrWhiteSpace(result.Message) ? "Operation completed successfully." : result.Message,
                Output = result.Output
            };

            return Ok(response);
        }

        private ActionResult<RepositoryCommandResponse> ExecuteRepositoryBranchCommand(string repositoryPath, string branchName, Func<string, string, GitCommandResult> executor)
        {
            if (string.IsNullOrWhiteSpace(repositoryPath) || string.IsNullOrWhiteSpace(branchName))
            {
                RepositoryCommandResponse errorResponse = new RepositoryCommandResponse
                {
                    Succeeded = false,
                    Message = "The repository path and branch name must be provided.",
                    Output = string.Empty
                };

                return BadRequest(errorResponse);
            }

            GitCommandResult result = executor(repositoryPath, branchName);

            if (!result.Succeeded)
            {
                RepositoryCommandResponse errorResponse = new RepositoryCommandResponse
                {
                    Succeeded = false,
                    Message = string.IsNullOrWhiteSpace(result.Message) ? "The operation could not be completed." : result.Message,
                    Output = result.Output
                };

                return BadRequest(errorResponse);
            }

            RepositoryCommandResponse response = new RepositoryCommandResponse
            {
                Succeeded = true,
                Message = string.IsNullOrWhiteSpace(result.Message) ? "Operation completed successfully." : result.Message,
                Output = result.Output
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

        public sealed class RepositoryCommandRequest
        {
            public string RepositoryPath { get; set; } = string.Empty;
        }

        public sealed class PublishBranchRequest
        {
            public string RepositoryPath { get; set; } = string.Empty;

            public string BranchName { get; set; } = string.Empty;
        }

        public sealed class SwitchBranchRequest
        {
            public string RepositoryPath { get; set; } = string.Empty;

            public string BranchName { get; set; } = string.Empty;

            public bool UseLinkedFlowBranch { get; set; }
        }

        public sealed class DeleteBranchRequest
        {
            public string RepositoryPath { get; set; } = string.Empty;

            public string BranchName { get; set; } = string.Empty;
        }

        public sealed class RepositoryCommandResponse
        {
            public bool Succeeded { get; set; }

            public string Message { get; set; } = string.Empty;

            public string Output { get; set; } = string.Empty;
        }

        public sealed class RemoteBranchesResponse
        {
            public bool Succeeded { get; set; }

            public string Message { get; set; } = string.Empty;

            public IReadOnlyCollection<RemoteBranchResponseItem> Branches { get; set; } = new List<RemoteBranchResponseItem>();
        }

        public sealed class RemoteBranchResponseItem
        {
            public string Name { get; set; } = string.Empty;

            public string RemoteName { get; set; } = string.Empty;

            public bool ExistsLocally { get; set; }
        }
    }
}
