using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using MyApp.Application.Abstractions;
using MyApp.Application.GitHubOAuth.DTOs;
using MyApp.Domain.Identity;

namespace MyApp.Application.GitHubOAuth.Queries.GetGitHubAccountDetails
{
    public sealed class GetGitHubAccountDetailsQueryHandler : IRequestHandler<GetGitHubAccountDetailsQuery, GitHubAccountDetailsDto>
    {
        private const string ProviderName = "GitHub";
        private readonly IUserExternalLoginRepository userExternalLoginRepository;
        private readonly IGitHubUserProfileClient userProfileClient;
        private readonly ILogger<GetGitHubAccountDetailsQueryHandler> logger;

        public GetGitHubAccountDetailsQueryHandler(
            IUserExternalLoginRepository userExternalLoginRepository,
            IGitHubUserProfileClient userProfileClient,
            ILogger<GetGitHubAccountDetailsQueryHandler> logger)
        {
            this.userExternalLoginRepository = userExternalLoginRepository;
            this.userProfileClient = userProfileClient;
            this.logger = logger;
        }

        public async Task<GitHubAccountDetailsDto> Handle(GetGitHubAccountDetailsQuery request, CancellationToken cancellationToken)
        {
            UserExternalLogin? login = await userExternalLoginRepository.GetAsync(request.UserId, ProviderName, cancellationToken);
            if (login == null)
            {
                GitHubAccountDetailsDto noConnection = new GitHubAccountDetailsDto(
                    false,
                    ProviderName,
                    string.Empty,
                    null,
                    false,
                    null,
                    string.Empty);
                return noConnection;
            }

            GitHubUserProfileInfo? profile = null;
            string? errorMessage = null;

            try
            {
                profile = await userProfileClient.GetProfileAsync(login.AccessToken, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Failed to retrieve GitHub profile information for user {UserId}.", request.UserId);
                errorMessage = "No se pudieron obtener los detalles de la cuenta vinculada desde GitHub.";
            }

            GitHubAccountDetailsDto dto = new GitHubAccountDetailsDto(
                true,
                login.Provider,
                login.ExternalUserId,
                login.ExpiresAt,
                login.SupportsRefresh,
                profile,
                errorMessage);

            return dto;
        }
    }
}
