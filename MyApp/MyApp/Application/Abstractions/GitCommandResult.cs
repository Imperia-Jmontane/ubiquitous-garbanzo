using System;

namespace MyApp.Application.Abstractions
{
    public sealed class GitCommandResult
    {
        public GitCommandResult(bool succeeded, string message, string output)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
            Output = output ?? string.Empty;
        }

        public bool Succeeded { get; }

        public string Message { get; }

        public string Output { get; }
    }
}
