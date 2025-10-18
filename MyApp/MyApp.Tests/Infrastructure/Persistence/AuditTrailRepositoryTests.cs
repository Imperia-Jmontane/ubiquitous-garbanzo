#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MyApp.Data;
using MyApp.Domain.Observability;
using MyApp.Infrastructure.Persistence;
using Xunit;

namespace MyApp.Tests.Infrastructure.Persistence
{
    public sealed class AuditTrailRepositoryTests
    {
        [Fact]
        public async Task AddAsync_ShouldPersistEntry()
        {
            DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            await using ApplicationDbContext dbContext = new ApplicationDbContext(options);
            AuditTrailRepository repository = new AuditTrailRepository(dbContext);
            AuditTrailEntry entry = new AuditTrailEntry(Guid.NewGuid(), "GitHubAccountLinked", "GitHub", "{}", DateTimeOffset.UtcNow, "corr");

            await repository.AddAsync(entry, CancellationToken.None);

            dbContext.AuditTrailEntries.Count().Should().Be(1);
            dbContext.AuditTrailEntries.Single().CorrelationId.Should().Be("corr");
        }
    }
}
