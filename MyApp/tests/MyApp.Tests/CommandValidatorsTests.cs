using System;
using FluentAssertions;
using FluentValidation.Results;
using MyApp.Application.Authentication.Commands;
using MyApp.Application.Authentication.Validators;

namespace MyApp.Tests
{
    public sealed class CommandValidatorsTests
    {
        [Fact]
        public void LinkGitHubAccountCommandValidator_Should_Fail_For_Invalid_Data()
        {
            LinkGitHubAccountCommandValidator validator = new LinkGitHubAccountCommandValidator();
            LinkGitHubAccountCommand command = new LinkGitHubAccountCommand(Guid.Empty, string.Empty, string.Empty, "invalid");

            ValidationResult result = validator.Validate(command);

            result.IsValid.Should().BeFalse();
        }

        [Fact]
        public void StartGitHubLinkCommandValidator_Should_Fail_For_Invalid_Redirect()
        {
            StartGitHubLinkCommandValidator validator = new StartGitHubLinkCommandValidator();
            StartGitHubLinkCommand command = new StartGitHubLinkCommand(Guid.Empty, "not-an-uri");

            ValidationResult result = validator.Validate(command);

            result.IsValid.Should().BeFalse();
        }

        [Fact]
        public void RefreshGitHubTokenCommandValidator_Should_Fail_For_EmptyUser()
        {
            RefreshGitHubTokenCommandValidator validator = new RefreshGitHubTokenCommandValidator();
            RefreshGitHubTokenCommand command = new RefreshGitHubTokenCommand(Guid.Empty);

            ValidationResult result = validator.Validate(command);

            result.IsValid.Should().BeFalse();
        }
    }
}
