using System.Threading;
using System.Threading.Tasks;
using MyApp.Application.GitHubOAuth.Models;

namespace MyApp.Application.Abstractions
{
    public interface IGitHubOAuthClient
    {
        Task<GitHubOAuthTokenResponse> ExchangeCodeAsync(GitHubCodeExchangeRequest request, CancellationToken cancellationToken);

        Task<GitHubOAuthTokenResponse> RefreshTokenAsync(GitHubTokenRefreshRequest request, CancellationToken cancellationToken);
    }
}
