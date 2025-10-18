#nullable enable
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MyApp.Infrastructure.Secrets;
using Xunit;

namespace MyApp.Tests.Infrastructure.Secrets
{
    public sealed class DataProtectedWritableSecretStoreTests : IDisposable
    {
        private readonly string rootPath;
        private readonly Mock<IHostEnvironment> hostEnvironmentMock;

        public DataProtectedWritableSecretStoreTests()
        {
            rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootPath);

            hostEnvironmentMock = new Mock<IHostEnvironment>();
            hostEnvironmentMock.SetupGet(environment => environment.ContentRootPath).Returns(rootPath);
        }

        [Fact]
        public async Task SetSecretAsync_ShouldPersistEncryptedSecret()
        {
            DataProtectedWritableSecretStore store = new DataProtectedWritableSecretStore(CreateProvider(), hostEnvironmentMock.Object, NullLogger<DataProtectedWritableSecretStore>.Instance);

            await store.SetSecretAsync("GitHubClientId", "client-value", CancellationToken.None);
            string? retrieved = await store.GetSecretAsync("GitHubClientId", CancellationToken.None);

            retrieved.Should().Be("client-value");

            string storagePath = Path.Combine(rootPath, "App_Data", "secret-store.json");
            string fileContent = await File.ReadAllTextAsync(storagePath, Encoding.UTF8);
            fileContent.Should().NotContain("client-value");
        }

        [Fact]
        public async Task GetSecretAsync_ShouldReturnNull_WhenStoreCorrupted()
        {
            DataProtectedWritableSecretStore store = new DataProtectedWritableSecretStore(CreateProvider(), hostEnvironmentMock.Object, NullLogger<DataProtectedWritableSecretStore>.Instance);

            string storagePath = Path.Combine(rootPath, "App_Data", "secret-store.json");
            Directory.CreateDirectory(Path.GetDirectoryName(storagePath)!);
            await File.WriteAllTextAsync(storagePath, "{ invalid", Encoding.UTF8);

            string? result = await store.GetSecretAsync("GitHubClientSecret", CancellationToken.None);

            result.Should().BeNull();
        }

        public void Dispose()
        {
            if (Directory.Exists(rootPath))
            {
                try
                {
                    Directory.Delete(rootPath, true);
                }
                catch
                {
                    // Ignored for cleanup.
                }
            }
        }

        private IDataProtectionProvider CreateProvider()
        {
            string keysPath = Path.Combine(rootPath, "keys");
            Directory.CreateDirectory(keysPath);
            return DataProtectionProvider.Create(new DirectoryInfo(keysPath));
        }
    }
}
