using System.Threading;
using System.Threading.Tasks;
using MyApp.Domain.Observability;

namespace MyApp.Application.Abstractions
{
    public interface IAuditTrailRepository
    {
        Task AddAsync(AuditTrailEntry entry, CancellationToken cancellationToken);
    }
}
