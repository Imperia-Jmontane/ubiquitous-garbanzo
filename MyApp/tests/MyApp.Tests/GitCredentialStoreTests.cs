using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Moq;
using MyApp.Application.Common.Interfaces;
using MyApp.Domain.ValueObjects;
using MyApp.Infrastructure.Security;

namespace MyApp.Tests
{
    public sealed class GitCredentialStoreTests
    {
        [Fact]
        public async Task Store_And_Retrieve_Should_Preserve_Token()
        {
            ISecretRepository secretRepository = new InMemorySecretRepository();
            IDataProtectionProvider dataProtectionProvider = new EphemeralDataProtectionProvider();
            Mock<IDateTimeProvider> dateTimeProvider = new Mock<IDateTimeProvider>();
            DateTimeOffset now = new DateTimeOffset(2025, 10, 17, 10, 0, 0, TimeSpan.Zero);
            dateTimeProvider.SetupGet(provider => provider.UtcNow).Returns(now);

            GitCredentialStore store = new GitCredentialStore(
                secretRepository,
                dataProtectionProvider,
                Options.Create(new GitCredentialStoreOptions { SecretNamePrefix = "git/github/" }),
                dateTimeProvider.Object);

            GitHubToken token = new GitHubToken("token", "refresh", now, now.AddHours(8), new List<string> { "repo", "read:user" });
            Guid userId = Guid.NewGuid();

            string secretName = await store.StoreAsync(userId, token, CancellationToken.None);
            secretName.Should().StartWith("git/github/");

            GitHubToken? retrieved = await store.GetAsync(secretName, CancellationToken.None);
            retrieved.Should().NotBeNull();
            retrieved!.AccessToken.Should().Be("token");
            retrieved.RefreshToken.Should().Be("refresh");
            retrieved.Scopes.Should().BeEquivalentTo(new List<string> { "repo", "read:user" });
        }
    }
}
