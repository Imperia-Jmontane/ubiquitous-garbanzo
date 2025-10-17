using System.Threading;
using System.Threading.Tasks;
using MyApp.Domain.Identity;
using MyApp.Domain.Tokens;

namespace MyApp.Domain.Services
{
    public interface IGitHubSecretStore
    {
        Task PersistTokenAsync(GitHubIdentity identity, GitHubToken token, CancellationToken cancellationToken);

        Task<GitHubToken?> RetrieveTokenAsync(GitHubIdentity identity, CancellationToken cancellationToken);
    }
}
