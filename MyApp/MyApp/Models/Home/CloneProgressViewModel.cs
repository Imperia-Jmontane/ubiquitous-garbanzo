using System;

namespace MyApp.Models.Home
{
    public sealed class CloneProgressViewModel
    {
        public CloneProgressViewModel(bool isActive, Guid operationId, string repositoryUrl, double percentage, string stage, string message)
        {
            IsActive = isActive;
            OperationId = operationId;
            RepositoryUrl = repositoryUrl ?? string.Empty;
            Percentage = percentage;
            Stage = stage ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool IsActive { get; }

        public Guid OperationId { get; }

        public string RepositoryUrl { get; }

        public double Percentage { get; }

        public string Stage { get; }

        public string Message { get; }

        public static CloneProgressViewModel CreateInactive()
        {
            return new CloneProgressViewModel(false, Guid.Empty, string.Empty, 0.0, string.Empty, string.Empty);
        }
    }
}
