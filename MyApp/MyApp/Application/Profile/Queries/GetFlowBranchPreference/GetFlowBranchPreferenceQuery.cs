using System;
using MediatR;
using MyApp.Application.Profile.DTOs;

namespace MyApp.Application.Profile.Queries.GetFlowBranchPreference
{
    public sealed class GetFlowBranchPreferenceQuery : IRequest<FlowBranchPreferenceDto>
    {
        public GetFlowBranchPreferenceQuery(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("The user identifier cannot be empty.", nameof(userId));
            }

            UserId = userId;
        }

        public Guid UserId { get; }
    }
}
