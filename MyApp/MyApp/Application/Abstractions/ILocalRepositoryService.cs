using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyApp.Domain.Repositories;

namespace MyApp.Application.Abstractions
{
    public interface ILocalRepositoryService
    {
        IReadOnlyCollection<LocalRepository> GetRepositories();

        CloneRepositoryResult CloneRepository(string repositoryUrl);

        Task<CloneRepositoryResult> CloneRepositoryAsync(string repositoryUrl, IProgress<RepositoryCloneProgress> progress, CancellationToken cancellationToken);

        DeleteRepositoryResult DeleteRepository(string repositoryPath);
    }
}
