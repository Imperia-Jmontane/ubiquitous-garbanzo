using System;
using MediatR;
using MyApp.Application.GitHubOAuth.DTOs;

namespace MyApp.Application.GitHubOAuth.Queries.GetGitHubAccountDetails
{
    public sealed class GetGitHubAccountDetailsQuery : IRequest<GitHubAccountDetailsDto>
    {
        public GetGitHubAccountDetailsQuery(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("The user identifier cannot be empty.", nameof(userId));
            }

            UserId = userId;
        }

        public Guid UserId { get; }
    }
}
