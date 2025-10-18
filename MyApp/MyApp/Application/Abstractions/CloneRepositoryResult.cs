using System;

namespace MyApp.Application.Abstractions
{
    public sealed class CloneRepositoryResult
    {
        public CloneRepositoryResult(bool succeeded, bool alreadyExists, string repositoryPath, string message)
        {
            Succeeded = succeeded;
            AlreadyExists = alreadyExists;
            RepositoryPath = repositoryPath ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }

        public bool AlreadyExists { get; }

        public string RepositoryPath { get; }

        public string Message { get; }
    }
}
