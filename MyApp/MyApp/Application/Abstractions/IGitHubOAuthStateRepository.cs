using System;
using System.Threading;
using System.Threading.Tasks;
using MyApp.Domain.Identity;

namespace MyApp.Application.Abstractions
{
    public interface IGitHubOAuthStateRepository
    {
        Task AddAsync(GitHubOAuthState state, CancellationToken cancellationToken);

        Task<GitHubOAuthState?> GetAsync(string state, CancellationToken cancellationToken);

        Task RemoveAsync(GitHubOAuthState state, CancellationToken cancellationToken);

        Task RemoveExpiredAsync(DateTimeOffset referenceTime, CancellationToken cancellationToken);
    }
}
