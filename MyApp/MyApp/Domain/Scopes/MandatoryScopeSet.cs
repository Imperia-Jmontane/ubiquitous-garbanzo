using System;
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

        public static bool AreSatisfiedBy(IEnumerable<string> candidateScopes)
        {
            if (candidateScopes == null)
            {
                throw new ArgumentNullException(nameof(candidateScopes));
            }

            HashSet<string> normalizedScopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string scope in candidateScopes)
            {
                if (string.IsNullOrWhiteSpace(scope))
                {
                    continue;
                }

                normalizedScopes.Add(scope.Trim());
            }

            foreach (string requiredScope in ingestionScopes)
            {
                if (!normalizedScopes.Contains(requiredScope))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
