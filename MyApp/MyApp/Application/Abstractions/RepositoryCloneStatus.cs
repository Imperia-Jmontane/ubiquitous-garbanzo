using System;

namespace MyApp.Application.Abstractions
{
    public sealed class RepositoryCloneStatus
    {
        public RepositoryCloneStatus(Guid operationId, string repositoryUrl, RepositoryCloneState state, double percentage, string stage, string message, DateTimeOffset lastUpdatedUtc)
        {
            OperationId = operationId;
            RepositoryUrl = repositoryUrl ?? string.Empty;
            State = state;
            Percentage = percentage;
            Stage = stage ?? string.Empty;
            Message = message ?? string.Empty;
            LastUpdatedUtc = lastUpdatedUtc;
        }

        public Guid OperationId { get; }

        public string RepositoryUrl { get; }

        public RepositoryCloneState State { get; }

        public double Percentage { get; }

        public string Stage { get; }

        public string Message { get; }

        public DateTimeOffset LastUpdatedUtc { get; }
    }
}
