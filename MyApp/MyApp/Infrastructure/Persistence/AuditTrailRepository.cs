using System.Threading;
using System.Threading.Tasks;
using MyApp.Application.Abstractions;
using MyApp.Data;
using MyApp.Domain.Observability;

namespace MyApp.Infrastructure.Persistence
{
    public sealed class AuditTrailRepository : IAuditTrailRepository
    {
        private readonly ApplicationDbContext dbContext;

        public AuditTrailRepository(ApplicationDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        public async Task AddAsync(AuditTrailEntry entry, CancellationToken cancellationToken)
        {
            await dbContext.AuditTrailEntries.AddAsync(entry, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
