using System.Threading;
using System.Threading.Tasks;

namespace MyApp.Application.GitHubOAuth.Configuration
{
    public interface IGitHubOAuthSettingsProvider
    {
        Task<GitHubOAuthSettings> GetSettingsAsync(CancellationToken cancellationToken);
    }
}
