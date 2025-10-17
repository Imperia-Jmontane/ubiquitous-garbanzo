using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyApp.Application.Authentication.Interfaces;
using MyApp.Domain.Entities;

namespace MyApp.Infrastructure.Persistence.Repositories
{
    public sealed class GitHubAccountLinkRepository : IGitHubAccountLinkRepository
    {
        private readonly ApplicationDbContext dbContext;

        public GitHubAccountLinkRepository(ApplicationDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        public async Task<GitHubAccountLink?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            return await dbContext.GitHubAccountLinks.FirstOrDefaultAsync(link => link.UserId == userId, cancellationToken);
        }

        public async Task AddAsync(GitHubAccountLink accountLink, CancellationToken cancellationToken)
        {
            await dbContext.GitHubAccountLinks.AddAsync(accountLink, cancellationToken);
        }

        public Task UpdateAsync(GitHubAccountLink accountLink, CancellationToken cancellationToken)
        {
            dbContext.GitHubAccountLinks.Update(accountLink);
            return Task.CompletedTask;
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
