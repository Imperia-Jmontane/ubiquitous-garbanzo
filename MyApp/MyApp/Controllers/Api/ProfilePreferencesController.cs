using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MyApp.Application.Profile.Commands.UpdateFlowBranchPreference;
using MyApp.Application.Profile.DTOs;
using MyApp.Models.Profile;

namespace MyApp.Controllers.Api
{
    [ApiController]
    [Route("api/profile")]
    public sealed class ProfilePreferencesController : ControllerBase
    {
        private readonly IMediator mediator;

        public ProfilePreferencesController(IMediator mediator)
        {
            this.mediator = mediator;
        }

        [HttpPost("flow-branches")]
        public async Task<ActionResult<FlowBranchPreferenceResponse>> UpdateFlowBranchPreference([FromBody] FlowBranchPreferenceRequest request, CancellationToken cancellationToken)
        {
            if (request == null || request.UserId == Guid.Empty)
            {
                FlowBranchPreferenceResponse errorResponse = new FlowBranchPreferenceResponse
                {
                    Succeeded = false,
                    Message = "El identificador de usuario es obligatorio.",
                    CreateLinkedBranches = false
                };

                return BadRequest(errorResponse);
            }

            UpdateFlowBranchPreferenceCommand command = new UpdateFlowBranchPreferenceCommand(request.UserId, request.CreateLinkedBranches);
            FlowBranchPreferenceDto result = await mediator.Send(command, cancellationToken);

            FlowBranchPreferenceResponse response = new FlowBranchPreferenceResponse
            {
                Succeeded = true,
                Message = result.CreateLinkedBranches ? "Las ramas vinculadas están activadas." : "Las ramas vinculadas están desactivadas.",
                CreateLinkedBranches = result.CreateLinkedBranches
            };

            return Ok(response);
        }
    }
}
