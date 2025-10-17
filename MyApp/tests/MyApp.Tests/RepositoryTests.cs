using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MyApp.Application.Authentication.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.ValueObjects;
using MyApp.Infrastructure.Persistence;
using MyApp.Infrastructure.Persistence.Repositories;

namespace MyApp.Tests
{
    public sealed class RepositoryTests
    {
        [Fact]
        public async Task UserExternalLoginRepository_Should_Enforce_Unique_State()
        {
            using SqliteConnection connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            await using ApplicationDbContext context = new ApplicationDbContext(options);
            await context.Database.EnsureCreatedAsync();

            IUserExternalLoginRepository repository = new UserExternalLoginRepository(context);

            UserExternalLogin first = new UserExternalLogin(Guid.NewGuid(), Guid.NewGuid(), "GitHub", string.Empty, "state-1", string.Empty, DateTimeOffset.UtcNow);
            await repository.AddAsync(first, CancellationToken.None);
            await repository.SaveChangesAsync(CancellationToken.None);

            UserExternalLogin second = new UserExternalLogin(Guid.NewGuid(), Guid.NewGuid(), "GitHub", string.Empty, "state-1", string.Empty, DateTimeOffset.UtcNow);
            await repository.AddAsync(second, CancellationToken.None);

            Func<Task> action = async () => await repository.SaveChangesAsync(CancellationToken.None);
            await action.Should().ThrowAsync<DbUpdateException>();
        }

        [Fact]
        public async Task GitHubAccountLinkRepository_Should_Persist_Identity()
        {
            using SqliteConnection connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            await using ApplicationDbContext context = new ApplicationDbContext(options);
            await context.Database.EnsureCreatedAsync();

            IGitHubAccountLinkRepository repository = new GitHubAccountLinkRepository(context);
            GitHubIdentity identity = new GitHubIdentity("123", "octocat", "The Octocat", "https://avatars/github.png");
            GitHubAccountLink link = new GitHubAccountLink(Guid.NewGuid(), identity, "secret/github/1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

            await repository.AddAsync(link, CancellationToken.None);
            await repository.SaveChangesAsync(CancellationToken.None);

            GitHubAccountLink? retrieved = await repository.GetByUserIdAsync(link.UserId, CancellationToken.None);
            retrieved.Should().NotBeNull();
            retrieved!.Identity.Login.Should().Be("octocat");
        }
    }
}
