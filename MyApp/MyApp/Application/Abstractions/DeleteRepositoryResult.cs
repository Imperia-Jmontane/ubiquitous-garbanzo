using System;

namespace MyApp.Application.Abstractions
{
    public sealed class DeleteRepositoryResult
    {
        public DeleteRepositoryResult(bool succeeded, bool notFound, string message)
        {
            Succeeded = succeeded;
            NotFound = notFound;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }

        public bool NotFound { get; }

        public string Message { get; }
    }
}
