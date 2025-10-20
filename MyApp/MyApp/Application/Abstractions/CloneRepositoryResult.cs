using System;

namespace MyApp.Application.Abstractions
{
    public sealed class CloneRepositoryResult
    {
        public CloneRepositoryResult(bool succeeded, bool alreadyExists, string repositoryPath, string message, bool wasCanceled)
        {
            Succeeded = succeeded;
            AlreadyExists = alreadyExists;
            RepositoryPath = repositoryPath ?? string.Empty;
            Message = message ?? string.Empty;
            WasCanceled = wasCanceled;
        }

        public bool Succeeded { get; }

        public bool AlreadyExists { get; }

        public string RepositoryPath { get; }

        public string Message { get; }

        public bool WasCanceled { get; }
    }
}
