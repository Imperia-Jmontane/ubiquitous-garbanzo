using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MyApp.Application.Abstractions;

namespace MyApp.Infrastructure.Secrets
{
    public sealed class ConfigurationSecretProvider : ISecretProvider
    {
        private readonly IConfiguration configuration;
        private readonly IWritableSecretStore writableSecretStore;

        public ConfigurationSecretProvider(IConfiguration configuration, IWritableSecretStore writableSecretStore)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (writableSecretStore == null)
            {
                throw new ArgumentNullException(nameof(writableSecretStore));
            }

            this.configuration = configuration;
            this.writableSecretStore = writableSecretStore;
        }

        public async Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("The secret name cannot be null or whitespace.", nameof(name));
            }

            string? storedValue = await writableSecretStore.GetSecretAsync(name, cancellationToken);
            if (!string.IsNullOrWhiteSpace(storedValue))
            {
                return storedValue;
            }

            string key = string.Concat("Secrets:", name);
            string? configurationValue = configuration[key];
            if (IsPlaceholder(configurationValue) || string.IsNullOrWhiteSpace(configurationValue))
            {
                return null;
            }

            return configurationValue;
        }

        private static bool IsPlaceholder(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmedValue = value.Trim();
            if (!trimmedValue.StartsWith("${", StringComparison.Ordinal))
            {
                return false;
            }

            return trimmedValue.EndsWith("}", StringComparison.Ordinal);
        }
    }
}
