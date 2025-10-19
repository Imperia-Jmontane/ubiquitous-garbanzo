using System;
using MyApp.Application.Abstractions;

namespace MyApp.Models.Home
{
    public sealed class CloneProgressViewModel
    {
        public CloneProgressViewModel(Guid operationId, string repositoryUrl, double percentage, string stage, string message, RepositoryCloneState state, DateTimeOffset lastUpdatedUtc)
        {
            OperationId = operationId;
            RepositoryUrl = repositoryUrl ?? string.Empty;
            Percentage = percentage;
            Stage = stage ?? string.Empty;
            Message = message ?? string.Empty;
            State = state;
            LastUpdatedUtc = lastUpdatedUtc;
        }

        public Guid OperationId { get; }

        public string RepositoryUrl { get; }

        public double Percentage { get; }

        public string Stage { get; }

        public string Message { get; }

        public RepositoryCloneState State { get; }

        public DateTimeOffset LastUpdatedUtc { get; }

        public bool IsActive
        {
            get
            {
                return State == RepositoryCloneState.Queued || State == RepositoryCloneState.Running;
            }
        }
    }
}
