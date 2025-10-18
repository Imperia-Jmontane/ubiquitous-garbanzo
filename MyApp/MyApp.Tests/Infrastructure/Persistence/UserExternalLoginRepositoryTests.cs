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
    public sealed class UserExternalLoginRepositoryTests
    {
        [Fact]
        public async Task AddAndGetAsync_ShouldPersistLogin()
        {
            DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            await using ApplicationDbContext dbContext = new ApplicationDbContext(options);
            UserExternalLoginRepository repository = new UserExternalLoginRepository(dbContext);
            Guid userId = Guid.NewGuid();
            UserExternalLogin login = new UserExternalLogin(userId, "GitHub", "node", "access", "refresh", DateTimeOffset.UtcNow.AddMinutes(10));

            await repository.AddAsync(login, CancellationToken.None);
            UserExternalLogin? stored = await repository.GetAsync(userId, "GitHub", CancellationToken.None);

            stored.Should().NotBeNull();
            stored!.ExternalUserId.Should().Be("node");
        }

        [Fact]
        public async Task UpdateAsync_ShouldPersistChanges()
        {
            DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            await using ApplicationDbContext dbContext = new ApplicationDbContext(options);
            UserExternalLoginRepository repository = new UserExternalLoginRepository(dbContext);
            Guid userId = Guid.NewGuid();
            UserExternalLogin login = new UserExternalLogin(userId, "GitHub", "node", "access", "refresh", DateTimeOffset.UtcNow.AddMinutes(10));
            await repository.AddAsync(login, CancellationToken.None);

            login.UpdateTokens("newAccess", "newRefresh", DateTimeOffset.UtcNow.AddMinutes(20));
            await repository.UpdateAsync(login, CancellationToken.None);

            UserExternalLogin? updated = await repository.GetAsync(userId, "GitHub", CancellationToken.None);
            updated!.AccessToken.Should().Be("newAccess");
        }
    }
}
