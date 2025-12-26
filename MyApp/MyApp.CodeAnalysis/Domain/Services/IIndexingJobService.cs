using System.Threading;
using System.Threading.Tasks;
using MyApp.CodeAnalysis.Application.DTOs;

namespace MyApp.CodeAnalysis.Domain.Services
{
    public interface IIndexingJobService
    {
        Task<long> QueueIndexingAsync(string repositoryId, CancellationToken ct);
        Task<IndexingJobStatus?> GetJobStatusAsync(string repositoryId, CancellationToken ct);
        Task<bool> CancelJobAsync(string repositoryId, CancellationToken ct);
    }
}
