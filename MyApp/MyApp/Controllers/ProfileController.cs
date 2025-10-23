using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MyApp.Application.GitHubOAuth.DTOs;
using MyApp.Application.GitHubOAuth.Queries.GetGitHubOAuthStatus;
using MyApp.Application.GitHubOAuth.Queries.GetGitHubAccountDetails;
using MyApp.Application.GitHubPersonalAccessToken.DTOs;
using MyApp.Application.GitHubPersonalAccessToken.Queries.GetGitHubPersonalAccessTokenStatus;
using MyApp.Models.Profile;

namespace MyApp.Controllers
{
    public sealed class ProfileController : Controller
    {
        private static readonly System.Guid DemoUserId = new System.Guid("11111111-1111-1111-1111-111111111111");
        private readonly IMediator mediator;

        public ProfileController(IMediator mediator)
        {
            this.mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            GitHubOAuthStatusDto status = await mediator.Send(new GetGitHubOAuthStatusQuery(), cancellationToken);
            GetGitHubAccountDetailsQuery accountDetailsQuery = new GetGitHubAccountDetailsQuery(DemoUserId);
            GitHubAccountDetailsDto accountDetails = await mediator.Send(accountDetailsQuery, cancellationToken);
            GitHubPersonalAccessTokenStatusDto patStatus = await mediator.Send(new GetGitHubPersonalAccessTokenStatusQuery(), cancellationToken);
            string redirectUri = Url.Action("GitHubCallback", "Auth", null, Request.Scheme) ?? string.Empty;
            ProfileViewModel viewModel = new ProfileViewModel
            {
                GitHubConfigured = status.IsConfigured,
                ClientIdPreview = CreateClientIdPreview(status.ClientId),
                GitHubScopes = new List<string>(status.Scopes),
                UserId = DemoUserId,
                GitHubRedirectUri = redirectUri,
                GitHubAccount = CreateGitHubAccountViewModel(accountDetails),
                PersonalAccessToken = CreatePersonalAccessTokenViewModel(patStatus)
            };

            return View(viewModel);
        }

        private static string CreateClientIdPreview(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return string.Empty;
            }

            if (clientId.Length <= 4)
            {
                return clientId;
            }

            string suffix = clientId.Substring(clientId.Length - 4);
            return string.Concat("••••", suffix);
        }

        private static GitHubAccountViewModel CreateGitHubAccountViewModel(GitHubAccountDetailsDto details)
        {
            GitHubAccountViewModel viewModel = new GitHubAccountViewModel
            {
                IsLinked = details.IsLinked,
                Provider = details.Provider,
                ExternalUserId = details.ExternalUserId,
                ExpiresAt = details.ExpiresAt,
                SupportsRefresh = details.SupportsRefresh
            };

            if (details.Profile != null)
            {
                viewModel.DetailsAvailable = true;
                viewModel.Login = details.Profile.Login;
                viewModel.Name = details.Profile.Name;
                viewModel.Email = details.Profile.Email;
                viewModel.AvatarUrl = details.Profile.AvatarUrl;
                viewModel.ProfileUrl = details.Profile.ProfileUrl;
                foreach (string organization in details.Profile.Organizations)
                {
                    viewModel.Organizations.Add(organization);
                }
            }

            if (!string.IsNullOrWhiteSpace(details.ProfileFetchError))
            {
                viewModel.Error = details.ProfileFetchError;
            }

            return viewModel;
        }

        private static GitHubPersonalAccessTokenViewModel CreatePersonalAccessTokenViewModel(GitHubPersonalAccessTokenStatusDto status)
        {
            GitHubPersonalAccessTokenViewModel viewModel = new GitHubPersonalAccessTokenViewModel
            {
                IsConfigured = status.IsConfigured,
                TokenStored = status.TokenStored,
                GenerationUrl = status.GenerationUrl,
                RequiredPermissions = new List<string>(status.RequiredPermissions)
            };

            if (status.Validation != null)
            {
                GitHubPersonalAccessTokenValidationViewModel validationViewModel = new GitHubPersonalAccessTokenValidationViewModel
                {
                    TokenAccepted = status.Validation.TokenAccepted,
                    HasRequiredPermissions = status.Validation.HasRequiredPermissions,
                    IsFineGrained = status.Validation.IsFineGrained,
                    RepositoryAccessConfirmed = status.Validation.RepositoryAccessConfirmed,
                    Login = status.Validation.Login,
                    FailureReason = status.Validation.FailureReason,
                    GrantedPermissions = new List<string>(status.Validation.GrantedPermissions),
                    MissingPermissions = new List<string>(status.Validation.MissingPermissions),
                    Warnings = new List<string>(status.Validation.Warnings)
                };

                viewModel.Validation = validationViewModel;
            }

            JsonSerializerOptions serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            viewModel.ValidationJson = JsonSerializer.Serialize(viewModel.Validation, serializerOptions);

            return viewModel;
        }
    }
}
