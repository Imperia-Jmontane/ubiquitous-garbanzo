using System.Collections.Generic;
using MyApp.Domain.Secrets;

namespace MyApp.Domain.Services
{
    public interface IRepositorySecretPolicyProvider
    {
        IReadOnlyCollection<RepositorySecretRule> GetSecretRules();
    }
}
