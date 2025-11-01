using System;
using MediatR;
using MyApp.Application.Profile.DTOs;

namespace MyApp.Application.Profile.Commands.UpdateFlowBranchPreference
{
    public sealed class UpdateFlowBranchPreferenceCommand : IRequest<FlowBranchPreferenceDto>
    {
        public UpdateFlowBranchPreferenceCommand(Guid userId, bool createLinkedBranches)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("The user identifier cannot be empty.", nameof(userId));
            }

            UserId = userId;
            CreateLinkedBranches = createLinkedBranches;
        }

        public Guid UserId { get; }

        public bool CreateLinkedBranches { get; }
    }
}
