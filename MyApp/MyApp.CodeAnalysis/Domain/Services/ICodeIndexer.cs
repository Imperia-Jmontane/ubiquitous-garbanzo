using System.Threading;
using System.Threading.Tasks;
using MyApp.CodeAnalysis.Application.DTOs;

namespace MyApp.CodeAnalysis.Domain.Services
{
    public interface ICodeIndexer
    {
        Task<IndexingResult> IndexSolutionAsync(long snapshotId, string solutionPath, CancellationToken ct);
        Task<IndexingResult> IndexProjectAsync(long snapshotId, string projectPath, CancellationToken ct);
        Task<IndexingResult> IndexFileAsync(long snapshotId, string filePath, CancellationToken ct);
    }
}
