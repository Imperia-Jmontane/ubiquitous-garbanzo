using System.Collections.Generic;
using MyApp.Application.GitHubPersonalAccessToken.Models;

namespace MyApp.Application.GitHubPersonalAccessToken.DTOs
{
    public sealed class GitHubPersonalAccessTokenValidationResultDto
    {
        public GitHubPersonalAccessTokenValidationResultDto(
            bool tokenAccepted,
            bool hasRequiredPermissions,
            bool isFineGrained,
            bool repositoryAccessConfirmed,
            string? login,
            IReadOnlyCollection<string> grantedPermissions,
            IReadOnlyCollection<string> missingPermissions,
            IReadOnlyCollection<string> warnings,
            string? failureReason)
        {
            TokenAccepted = tokenAccepted;
            HasRequiredPermissions = hasRequiredPermissions;
            IsFineGrained = isFineGrained;
            RepositoryAccessConfirmed = repositoryAccessConfirmed;
            Login = login;
            GrantedPermissions = grantedPermissions;
            MissingPermissions = missingPermissions;
            Warnings = warnings;
            FailureReason = failureReason;
        }

        public bool TokenAccepted { get; }

        public bool HasRequiredPermissions { get; }

        public bool IsFineGrained { get; }

        public bool RepositoryAccessConfirmed { get; }

        public string? Login { get; }

        public IReadOnlyCollection<string> GrantedPermissions { get; }

        public IReadOnlyCollection<string> MissingPermissions { get; }

        public IReadOnlyCollection<string> Warnings { get; }

        public string? FailureReason { get; }

        public static GitHubPersonalAccessTokenValidationResultDto FromInspection(GitHubPersonalAccessTokenInspectionResult inspection)
        {
            return new GitHubPersonalAccessTokenValidationResultDto(
                inspection.TokenAccepted,
                inspection.HasRequiredPermissions,
                inspection.IsFineGrained,
                inspection.RepositoryAccessConfirmed,
                inspection.Login,
                inspection.GrantedPermissions,
                inspection.MissingPermissions,
                inspection.Warnings,
                inspection.FailureReason);
        }
    }
}
