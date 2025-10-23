using System.Collections.Generic;

namespace MyApp.Application.GitHubPersonalAccessToken.Models
{
    public sealed class GitHubPersonalAccessTokenInspectionResult
    {
        public GitHubPersonalAccessTokenInspectionResult(
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
    }
}
