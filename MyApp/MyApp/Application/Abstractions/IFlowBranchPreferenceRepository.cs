using System;
using System.Threading;
using System.Threading.Tasks;
using MyApp.Domain.Identity;

namespace MyApp.Application.Abstractions
{
    public interface IFlowBranchPreferenceRepository
    {
        Task<FlowBranchPreference?> GetAsync(Guid userId, CancellationToken cancellationToken);

        Task AddAsync(FlowBranchPreference preference, CancellationToken cancellationToken);

        Task UpdateAsync(FlowBranchPreference preference, CancellationToken cancellationToken);
    }
}
