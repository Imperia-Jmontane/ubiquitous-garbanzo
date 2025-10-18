using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyApp.Application.Abstractions;

namespace MyApp.Infrastructure.Secrets
{
    public sealed class DataProtectedWritableSecretStore : IWritableSecretStore
    {
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        private readonly string storagePath;
        private readonly IDataProtector dataProtector;
        private readonly ILogger<DataProtectedWritableSecretStore> logger;
        private readonly SemaphoreSlim semaphore;

        public DataProtectedWritableSecretStore(IDataProtectionProvider dataProtectionProvider, IHostEnvironment hostEnvironment, ILogger<DataProtectedWritableSecretStore> logger)
        {
            if (dataProtectionProvider == null)
            {
                throw new ArgumentNullException(nameof(dataProtectionProvider));
            }

            if (hostEnvironment == null)
            {
                throw new ArgumentNullException(nameof(hostEnvironment));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            string appDataDirectory = Path.Combine(hostEnvironment.ContentRootPath, "App_Data");
            storagePath = Path.Combine(appDataDirectory, "secret-store.json");
            dataProtector = dataProtectionProvider.CreateProtector("MyApp.Infrastructure.Secrets.DataProtectedWritableSecretStore");
            this.logger = logger;
            semaphore = new SemaphoreSlim(1, 1);
        }

        public async Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("The secret name cannot be null or whitespace.", nameof(name));
            }

            await semaphore.WaitAsync(cancellationToken);
            try
            {
                Dictionary<string, string> encryptedSecrets = await ReadEncryptedSecretsAsync(cancellationToken);
                if (!encryptedSecrets.TryGetValue(name, out string? encryptedValue))
                {
                    return null;
                }

                try
                {
                    string decryptedValue = dataProtector.Unprotect(encryptedValue);
                    return decryptedValue;
                }
                catch (CryptographicException exception)
                {
                    logger.LogError(exception, "Failed to decrypt secret {SecretName}.", name);
                    return null;
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task SetSecretAsync(string name, string value, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("The secret name cannot be null or whitespace.", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("The secret value cannot be null or whitespace.", nameof(value));
            }

            await semaphore.WaitAsync(cancellationToken);
            try
            {
                Dictionary<string, string> encryptedSecrets = await ReadEncryptedSecretsAsync(cancellationToken);
                string encryptedValue = dataProtector.Protect(value);
                encryptedSecrets[name] = encryptedValue;
                await WriteEncryptedSecretsAsync(encryptedSecrets, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task<Dictionary<string, string>> ReadEncryptedSecretsAsync(CancellationToken cancellationToken)
        {
            if (!File.Exists(storagePath))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                using FileStream fileStream = new FileStream(storagePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                Dictionary<string, string>? encryptedSecrets = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(fileStream, SerializerOptions, cancellationToken);
                if (encryptedSecrets == null)
                {
                    return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                return new Dictionary<string, string>(encryptedSecrets, StringComparer.OrdinalIgnoreCase);
            }
            catch (JsonException exception)
            {
                logger.LogError(exception, "Corrupted secret store detected at {StoragePath}.", storagePath);
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private async Task WriteEncryptedSecretsAsync(IDictionary<string, string> encryptedSecrets, CancellationToken cancellationToken)
        {
            string? directoryPath = Path.GetDirectoryName(storagePath);
            if (directoryPath == null)
            {
                throw new InvalidOperationException("The secret store directory path could not be determined.");
            }

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            using FileStream fileStream = new FileStream(storagePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await JsonSerializer.SerializeAsync(fileStream, encryptedSecrets, SerializerOptions, cancellationToken);
            await fileStream.FlushAsync(cancellationToken);
        }
    }
}
