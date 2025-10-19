using System;

namespace MyApp.Application.Abstractions
{
    public sealed class RepositoryCloneProgress
    {
        public RepositoryCloneProgress(double percentage, string stage, string details)
        {
            Percentage = percentage;
            Stage = stage ?? string.Empty;
            Details = details ?? string.Empty;
        }

        public double Percentage { get; }

        public string Stage { get; }

        public string Details { get; }
    }
}
