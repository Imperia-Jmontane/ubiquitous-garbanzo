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

        bool RepositoryExists(string repositoryUrl);

        GitCommandResult FetchRepository(string repositoryPath);

        GitCommandResult PullRepository(string repositoryPath);

        GitCommandResult PushRepository(string repositoryPath);

        GitCommandResult PublishBranch(string repositoryPath, string branchName);

        GitCommandResult SwitchBranch(string repositoryPath, string branchName, bool useLinkedFlowBranch);

        GitCommandResult CommitRepository(string repositoryPath);

        GitCommandResult DeleteBranch(string repositoryPath, string branchName);

        RemoteBranchQueryResult GetRemoteBranches(string repositoryPath, string searchTerm);
    }
}
