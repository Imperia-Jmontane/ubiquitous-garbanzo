using System;
using System.Collections.Generic;

namespace MyApp.Application.Abstractions
{
    public interface IRepositoryCloneCoordinator
    {
        RepositoryCloneTicket QueueClone(string repositoryUrl);

        bool TryGetStatus(Guid operationId, out RepositoryCloneStatus? status);

        IReadOnlyCollection<RepositoryCloneStatus> GetActiveClones();

        RepositoryCloneCancellationResult CancelClone(Guid operationId);
    }
}
