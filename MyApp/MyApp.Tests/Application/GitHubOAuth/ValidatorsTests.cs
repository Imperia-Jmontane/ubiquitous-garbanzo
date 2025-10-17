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
        public void LinkGitHubAccountCommandValidator_ShouldFailForInvalidRedirect()
        {
            LinkGitHubAccountCommandValidator validator = new LinkGitHubAccountCommandValidator();
            LinkGitHubAccountCommand command = new LinkGitHubAccountCommand(Guid.NewGuid(), "code", "state", "not-a-url");

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
