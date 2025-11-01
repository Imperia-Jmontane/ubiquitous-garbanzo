using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyApp.Application.Abstractions;
using MyApp.Application.Profile.DTOs;
using MyApp.Domain.Identity;

namespace MyApp.Application.Profile.Commands.UpdateFlowBranchPreference
{
    public sealed class UpdateFlowBranchPreferenceCommandHandler : IRequestHandler<UpdateFlowBranchPreferenceCommand, FlowBranchPreferenceDto>
    {
        private readonly IFlowBranchPreferenceRepository repository;

        public UpdateFlowBranchPreferenceCommandHandler(IFlowBranchPreferenceRepository repository)
        {
            this.repository = repository;
        }

        public async Task<FlowBranchPreferenceDto> Handle(UpdateFlowBranchPreferenceCommand request, CancellationToken cancellationToken)
        {
            FlowBranchPreference? preference = await repository.GetAsync(request.UserId, cancellationToken);

            if (preference == null)
            {
                FlowBranchPreference createdPreference = new FlowBranchPreference(request.UserId, request.CreateLinkedBranches);
                await repository.AddAsync(createdPreference, cancellationToken);
                return new FlowBranchPreferenceDto(createdPreference.CreateLinkedBranches);
            }

            preference.SetCreateLinkedBranches(request.CreateLinkedBranches);
            await repository.UpdateAsync(preference, cancellationToken);
            return new FlowBranchPreferenceDto(preference.CreateLinkedBranches);
        }
    }
}
