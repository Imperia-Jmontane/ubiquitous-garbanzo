using System.Collections.Generic;

namespace MyApp.Application.GitHubPersonalAccessToken
{
    public static class GitHubPersonalAccessTokenRequirements
    {
        public const string GenerationUrl = "https://github.com/settings/tokens";

        public static readonly IReadOnlyCollection<string> RequiredScopes = new string[]
        {
            "repo",
            "workflow",
            "read:org"
        };

        public static readonly IReadOnlyCollection<string> DisplayPermissions = new string[]
        {
            "repo (PAT clásico) o Contents: read en un PAT con permisos afinados",
            "workflow (PAT clásico) o Actions: read & write",
            "read:org si necesitas repos privados de organizaciones"
        };
    }
}
