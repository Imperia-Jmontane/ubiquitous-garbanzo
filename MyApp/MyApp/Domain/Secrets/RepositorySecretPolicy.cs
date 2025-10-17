using System.Collections.Generic;
using MyApp.Domain.Services;

namespace MyApp.Domain.Secrets
{
    public sealed class RepositorySecretPolicy : IRepositorySecretPolicyProvider
    {
        public IReadOnlyCollection<RepositorySecretRule> GetSecretRules()
        {
            return RepositorySecretRequirements.Rules;
        }
    }
}
