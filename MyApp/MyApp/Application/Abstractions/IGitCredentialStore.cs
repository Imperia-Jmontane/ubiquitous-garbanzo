using System.Threading;
using System.Threading.Tasks;
using MyApp.Application.GitHubOAuth.Models;

namespace MyApp.Application.Abstractions
{
    public interface IGitCredentialStore
    {
        Task<GitHubOAuthClientCredentials> GetClientCredentialsAsync(CancellationToken cancellationToken);
    }
}
