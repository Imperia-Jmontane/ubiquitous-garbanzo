using System;

namespace MyApp.Application.Abstractions
{
    public sealed class RepositoryCloneTicket
    {
        public RepositoryCloneTicket(Guid operationId, string repositoryUrl, bool alreadyCloned, bool enqueued)
        {
            OperationId = operationId;
            RepositoryUrl = repositoryUrl ?? string.Empty;
            AlreadyCloned = alreadyCloned;
            Enqueued = enqueued;
        }

        public Guid OperationId { get; }

        public string RepositoryUrl { get; }

        public bool AlreadyCloned { get; }

        public bool Enqueued { get; }

        public bool HasOperation
        {
            get
            {
                return OperationId != Guid.Empty;
            }
        }
    }
}
