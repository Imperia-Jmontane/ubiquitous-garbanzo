using System.Collections.Generic;
using MyApp.Domain.Repositories;

namespace MyApp.Application.Abstractions
{
    public interface ILocalRepositoryService
    {
        IReadOnlyCollection<LocalRepository> GetRepositories();

        CloneRepositoryResult CloneRepository(string repositoryUrl);
    }
}
