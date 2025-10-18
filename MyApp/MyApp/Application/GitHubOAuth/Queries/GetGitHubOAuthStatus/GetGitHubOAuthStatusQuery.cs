using MediatR;
using MyApp.Application.GitHubOAuth.DTOs;

namespace MyApp.Application.GitHubOAuth.Queries.GetGitHubOAuthStatus
{
    public sealed class GetGitHubOAuthStatusQuery : IRequest<GitHubOAuthStatusDto>
    {
    }
}
