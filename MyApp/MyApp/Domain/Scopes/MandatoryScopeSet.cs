using System.Collections.Generic;

namespace MyApp.Domain.Scopes
{
    public static class MandatoryScopeSet
    {
        private static readonly IReadOnlyCollection<string> ingestionScopes = new List<string>
        {
            GitHubScopes.Repo,
            GitHubScopes.Workflow,
            GitHubScopes.ReadUser
        };

        public static IReadOnlyCollection<string> IngestionScopes
        {
            get { return ingestionScopes; }
        }
    }
}
