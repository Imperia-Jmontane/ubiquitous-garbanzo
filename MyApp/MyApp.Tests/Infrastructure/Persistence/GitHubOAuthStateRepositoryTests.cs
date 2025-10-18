#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MyApp.Data;
using MyApp.Domain.Identity;
using MyApp.Infrastructure.Persistence;
using Xunit;

namespace MyApp.Tests.Infrastructure.Persistence
{
    public sealed class GitHubOAuthStateRepositoryTests
    {
        [Fact]
        public async Task AddAsync_ShouldPersistState()
        {
            DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            await using ApplicationDbContext dbContext = new ApplicationDbContext(options);
            GitHubOAuthStateRepository repository = new GitHubOAuthStateRepository(dbContext);
            GitHubOAuthState state = new GitHubOAuthState(Guid.NewGuid(), "state", "https://app/callback", DateTimeOffset.UtcNow.AddMinutes(5));

            await repository.AddAsync(state, CancellationToken.None);

            dbContext.GitHubOAuthStates.Single().State.Should().Be("state");
        }

        [Fact]
        public async Task RemoveExpiredAsync_ShouldDeleteOnlyExpiredStates()
        {
            DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            await using ApplicationDbContext dbContext = new ApplicationDbContext(options);
            GitHubOAuthStateRepository repository = new GitHubOAuthStateRepository(dbContext);
            GitHubOAuthState expired = new GitHubOAuthState(Guid.NewGuid(), "expired", "https://app/callback", DateTimeOffset.UtcNow.AddMinutes(1));
            GitHubOAuthState active = new GitHubOAuthState(Guid.NewGuid(), "active", "https://app/callback", DateTimeOffset.UtcNow.AddMinutes(10));
            await dbContext.GitHubOAuthStates.AddRangeAsync(expired, active);
            await dbContext.SaveChangesAsync();

            await repository.RemoveExpiredAsync(DateTimeOffset.UtcNow.AddMinutes(5), CancellationToken.None);

            dbContext.GitHubOAuthStates.Count().Should().Be(1);
            dbContext.GitHubOAuthStates.Single().State.Should().Be("active");
        }
    }
}
