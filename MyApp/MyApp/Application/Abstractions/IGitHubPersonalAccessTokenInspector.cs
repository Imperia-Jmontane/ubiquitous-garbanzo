using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyApp.Application.GitHubPersonalAccessToken.Models;

namespace MyApp.Application.Abstractions
{
    public interface IGitHubPersonalAccessTokenInspector
    {
        Task<GitHubPersonalAccessTokenInspectionResult> InspectAsync(string token, IReadOnlyCollection<string> requiredScopes, CancellationToken cancellationToken);
    }
}
