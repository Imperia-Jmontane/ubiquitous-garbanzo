using System.Threading;
using System.Threading.Tasks;
using MyApp.Application.CodeAnalysis.DTOs;

namespace MyApp.Domain.CodeAnalysis
{
    public interface ICodeIndexer
    {
        Task<IndexingResult> IndexSolutionAsync(long snapshotId, string solutionPath, CancellationToken ct);
        Task<IndexingResult> IndexProjectAsync(long snapshotId, string projectPath, CancellationToken ct);
        Task<IndexingResult> IndexFileAsync(long snapshotId, string filePath, CancellationToken ct);
    }
}
