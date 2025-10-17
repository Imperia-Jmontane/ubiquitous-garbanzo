using System.Threading;
using System.Threading.Tasks;
using MyApp.Application.Authentication.Models;

namespace MyApp.Application.Authentication.Interfaces
{
    public interface IGitHubOAuthClient
    {
        GitHubAuthorizationInfo CreateAuthorizationInfo(string state, string redirectUri);

        Task<GitHubOAuthSession> ExchangeCodeForTokenAsync(string code, string redirectUri, CancellationToken cancellationToken);

        Task<GitHubOAuthSession> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken);
    }
}
