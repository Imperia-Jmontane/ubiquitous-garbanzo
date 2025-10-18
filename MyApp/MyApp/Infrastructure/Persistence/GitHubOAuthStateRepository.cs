using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyApp.Application.Abstractions;
using MyApp.Data;
using MyApp.Domain.Identity;

namespace MyApp.Infrastructure.Persistence
{
    public sealed class GitHubOAuthStateRepository : IGitHubOAuthStateRepository
    {
        private readonly ApplicationDbContext dbContext;

        public GitHubOAuthStateRepository(ApplicationDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        public async Task AddAsync(GitHubOAuthState state, CancellationToken cancellationToken)
        {
            await dbContext.GitHubOAuthStates.AddAsync(state, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<GitHubOAuthState?> GetAsync(string state, CancellationToken cancellationToken)
        {
            return await dbContext.GitHubOAuthStates.SingleOrDefaultAsync(entity => entity.State == state, cancellationToken);
        }

        public async Task RemoveAsync(GitHubOAuthState state, CancellationToken cancellationToken)
        {
            dbContext.GitHubOAuthStates.Remove(state);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task RemoveExpiredAsync(DateTimeOffset referenceTime, CancellationToken cancellationToken)
        {
            List<GitHubOAuthState> expiredStates = await dbContext.GitHubOAuthStates
                .Where(entity => entity.ExpiresAt <= referenceTime)
                .ToListAsync(cancellationToken);

            if (expiredStates.Count == 0)
            {
                return;
            }

            dbContext.GitHubOAuthStates.RemoveRange(expiredStates);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
