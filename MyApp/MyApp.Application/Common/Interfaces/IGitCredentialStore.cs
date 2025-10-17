using System;
using System.Threading;
using System.Threading.Tasks;
using MyApp.Domain.ValueObjects;

namespace MyApp.Application.Common.Interfaces
{
    public interface IGitCredentialStore
    {
        Task<string> StoreAsync(Guid userId, GitHubToken token, CancellationToken cancellationToken);

        Task UpdateAsync(string secretName, GitHubToken token, CancellationToken cancellationToken);

        Task<GitHubToken?> GetAsync(string secretName, CancellationToken cancellationToken);
    }
}
