using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyApp.Application.Common.Interfaces;

namespace MyApp.Infrastructure.Security
{
    public sealed class InMemorySecretRepository : ISecretRepository
    {
        private readonly ConcurrentDictionary<string, string> secrets;

        public InMemorySecretRepository()
        {
            secrets = new ConcurrentDictionary<string, string>();
        }

        public Task<string> SetSecretAsync(string name, string value, IDictionary<string, string> tags, CancellationToken cancellationToken)
        {
            secrets[name] = value;
            return Task.FromResult(name);
        }

        public Task UpdateSecretAsync(string name, string value, IDictionary<string, string> tags, CancellationToken cancellationToken)
        {
            secrets[name] = value;
            return Task.CompletedTask;
        }

        public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken)
        {
            if (secrets.TryGetValue(name, out string? value))
            {
                return Task.FromResult<string?>(value);
            }

            return Task.FromResult<string?>(null);
        }
    }
}
