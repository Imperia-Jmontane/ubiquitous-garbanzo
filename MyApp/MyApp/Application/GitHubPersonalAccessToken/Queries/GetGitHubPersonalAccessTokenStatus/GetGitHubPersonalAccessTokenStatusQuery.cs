using MediatR;
using MyApp.Application.GitHubPersonalAccessToken.DTOs;

namespace MyApp.Application.GitHubPersonalAccessToken.Queries.GetGitHubPersonalAccessTokenStatus
{
    public sealed class GetGitHubPersonalAccessTokenStatusQuery : IRequest<GitHubPersonalAccessTokenStatusDto>
    {
    }
}
