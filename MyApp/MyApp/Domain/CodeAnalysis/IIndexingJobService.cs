using System.Threading;
using System.Threading.Tasks;
using MyApp.Application.CodeAnalysis.DTOs;

namespace MyApp.Domain.CodeAnalysis
{
    public interface IIndexingJobService
    {
        Task<long> QueueIndexingAsync(string repositoryId, CancellationToken ct);
        Task<IndexingJobStatus?> GetJobStatusAsync(string repositoryId, CancellationToken ct);
        Task<bool> CancelJobAsync(string repositoryId, CancellationToken ct);
    }
}
