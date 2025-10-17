using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Security.KeyVault.Secrets;
using MyApp.Application.Common.Interfaces;

namespace MyApp.Infrastructure.Security
{
    public sealed class KeyVaultSecretRepository : ISecretRepository
    {
        private readonly SecretClient secretClient;

        public KeyVaultSecretRepository(SecretClient secretClient)
        {
            this.secretClient = secretClient;
        }

        public async Task<string> SetSecretAsync(string name, string value, IDictionary<string, string> tags, CancellationToken cancellationToken)
        {
            KeyVaultSecret secret = new KeyVaultSecret(name, value);

            foreach (KeyValuePair<string, string> tag in tags)
            {
                secret.Properties.Tags[tag.Key] = tag.Value;
            }

            await secretClient.SetSecretAsync(secret, cancellationToken);

            return name;
        }

        public async Task UpdateSecretAsync(string name, string value, IDictionary<string, string> tags, CancellationToken cancellationToken)
        {
            KeyVaultSecret secret = new KeyVaultSecret(name, value);

            foreach (KeyValuePair<string, string> tag in tags)
            {
                secret.Properties.Tags[tag.Key] = tag.Value;
            }

            await secretClient.SetSecretAsync(secret, cancellationToken);
        }

        public async Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken)
        {
            try
            {
                Response<KeyVaultSecret> response = await secretClient.GetSecretAsync(name, cancellationToken: cancellationToken);
                return response.Value.Value;
            }
            catch (RequestFailedException exception) when (exception.Status == 404)
            {
                return null;
            }
        }
    }
}
