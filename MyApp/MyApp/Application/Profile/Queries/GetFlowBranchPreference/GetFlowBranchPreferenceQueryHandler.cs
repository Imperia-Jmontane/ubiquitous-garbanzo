using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyApp.Application.Abstractions;
using MyApp.Application.Profile.DTOs;
using MyApp.Domain.Identity;

namespace MyApp.Application.Profile.Queries.GetFlowBranchPreference
{
    public sealed class GetFlowBranchPreferenceQueryHandler : IRequestHandler<GetFlowBranchPreferenceQuery, FlowBranchPreferenceDto>
    {
        private readonly IFlowBranchPreferenceRepository repository;

        public GetFlowBranchPreferenceQueryHandler(IFlowBranchPreferenceRepository repository)
        {
            this.repository = repository;
        }

        public async Task<FlowBranchPreferenceDto> Handle(GetFlowBranchPreferenceQuery request, CancellationToken cancellationToken)
        {
            FlowBranchPreference? preference = await repository.GetAsync(request.UserId, cancellationToken);

            if (preference == null)
            {
                return new FlowBranchPreferenceDto(false);
            }

            return new FlowBranchPreferenceDto(preference.CreateLinkedBranches);
        }
    }
}
