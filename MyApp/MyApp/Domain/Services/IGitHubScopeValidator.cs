using System.Threading;
using System.Threading.Tasks;
using MyApp.Domain.Tokens;
using MyApp.Domain.Scopes;

namespace MyApp.Domain.Services
{
    public interface IGitHubScopeValidator
    {
        Task<ScopeValidationResult> ValidateAsync(GitHubToken token, CancellationToken cancellationToken);
    }
}
