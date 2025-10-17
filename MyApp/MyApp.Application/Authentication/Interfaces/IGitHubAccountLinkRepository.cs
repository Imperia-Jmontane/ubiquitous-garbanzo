using System;
using System.Threading;
using System.Threading.Tasks;
using MyApp.Domain.Entities;

namespace MyApp.Application.Authentication.Interfaces
{
    public interface IGitHubAccountLinkRepository
    {
        Task<GitHubAccountLink?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);

        Task AddAsync(GitHubAccountLink accountLink, CancellationToken cancellationToken);

        Task UpdateAsync(GitHubAccountLink accountLink, CancellationToken cancellationToken);

        Task SaveChangesAsync(CancellationToken cancellationToken);
    }
}
