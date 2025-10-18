using System;
using MediatR;
using MyApp.Application.GitHubOAuth.DTOs;

namespace MyApp.Application.GitHubOAuth.Commands.LinkGitHubAccount
{
    public sealed class LinkGitHubAccountCommand : IRequest<LinkGitHubAccountResultDto>
    {
        public LinkGitHubAccountCommand(Guid userId, string code, string state)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("The user identifier cannot be empty.", nameof(userId));
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("The authorization code cannot be null or whitespace.", nameof(code));
            }

            if (string.IsNullOrWhiteSpace(state))
            {
                throw new ArgumentException("The state value cannot be null or whitespace.", nameof(state));
            }

            UserId = userId;
            Code = code;
            State = state;
        }

        public Guid UserId { get; }

        public string Code { get; }

        public string State { get; }

    }
}
