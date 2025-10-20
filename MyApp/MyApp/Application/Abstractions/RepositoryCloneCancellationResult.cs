using System;

namespace MyApp.Application.Abstractions
{
    public sealed class RepositoryCloneCancellationResult
    {
        public RepositoryCloneCancellationResult(bool succeeded, bool notFound, bool alreadyCompleted, RepositoryCloneStatus? status, string message)
        {
            Succeeded = succeeded;
            NotFound = notFound;
            AlreadyCompleted = alreadyCompleted;
            Status = status;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }

        public bool NotFound { get; }

        public bool AlreadyCompleted { get; }

        public RepositoryCloneStatus? Status { get; }

        public string Message { get; }
    }
}
