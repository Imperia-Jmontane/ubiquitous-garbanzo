using System;
using FluentAssertions;
using FluentValidation.Results;
using MyApp.Application.GitHubOAuth.Commands.LinkGitHubAccount;
using MyApp.Application.GitHubOAuth.Commands.RefreshGitHubToken;
using Xunit;

namespace MyApp.Tests.Application.GitHubOAuth
{
    public sealed class ValidatorsTests
    {
        [Fact]
        public void LinkGitHubAccountCommandValidator_ShouldFailForMissingState()
        {
            LinkGitHubAccountCommandValidator validator = new LinkGitHubAccountCommandValidator();
            string longState = new string('x', 300);
            LinkGitHubAccountCommand command = new LinkGitHubAccountCommand(Guid.NewGuid(), "code", longState);

            ValidationResult result = validator.Validate(command);
            result.IsValid.Should().BeFalse();
        }

        [Fact]
        public void RefreshGitHubTokenCommandValidator_ShouldFailForInvalidRedirect()
        {
            RefreshGitHubTokenCommandValidator validator = new RefreshGitHubTokenCommandValidator();
            RefreshGitHubTokenCommand command = new RefreshGitHubTokenCommand(Guid.NewGuid(), "state", "invalid");

            ValidationResult result = validator.Validate(command);
            result.IsValid.Should().BeFalse();
        }
    }
}
