using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyApp.Application.Abstractions;
using MyApp.Data;
using MyApp.Domain.Identity;

namespace MyApp.Infrastructure.Persistence
{
    public sealed class FlowBranchPreferenceRepository : IFlowBranchPreferenceRepository
    {
        private readonly ApplicationDbContext dbContext;

        public FlowBranchPreferenceRepository(ApplicationDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        public async Task<FlowBranchPreference?> GetAsync(Guid userId, CancellationToken cancellationToken)
        {
            return await dbContext.FlowBranchPreferences.SingleOrDefaultAsync(preference => preference.UserId == userId, cancellationToken);
        }

        public async Task AddAsync(FlowBranchPreference preference, CancellationToken cancellationToken)
        {
            await dbContext.FlowBranchPreferences.AddAsync(preference, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateAsync(FlowBranchPreference preference, CancellationToken cancellationToken)
        {
            dbContext.FlowBranchPreferences.Update(preference);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
