using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MyApp.Application.GitHubOAuth.DTOs;
using MyApp.Application.GitHubOAuth.Queries.GetGitHubOAuthStatus;
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
            string redirectUri = Url.Action("GitHubCallback", "Auth", null, Request.Scheme) ?? string.Empty;
            ProfileViewModel viewModel = new ProfileViewModel
            {
                GitHubConfigured = status.IsConfigured,
                ClientIdPreview = CreateClientIdPreview(status.ClientId),
                GitHubScopes = new List<string>(status.Scopes),
                UserId = DemoUserId,
                GitHubRedirectUri = redirectUri
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
    }
}
