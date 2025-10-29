using System;
using System.Collections.Generic;

namespace MyApp.Domain.Repositories
{
    public sealed class RemoteBranchQueryResult
    {
        public RemoteBranchQueryResult(bool succeeded, string message, IReadOnlyCollection<RepositoryRemoteBranch> branches)
        {
            if (branches == null)
            {
                throw new ArgumentNullException(nameof(branches));
            }

            Succeeded = succeeded;
            Message = message ?? string.Empty;
            Branches = branches;
        }

        public bool Succeeded { get; }

        public string Message { get; }

        public IReadOnlyCollection<RepositoryRemoteBranch> Branches { get; }
    }
}

