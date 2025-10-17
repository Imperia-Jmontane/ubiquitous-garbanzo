using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using MyApp.Application.Common.Interfaces;

namespace MyApp.Infrastructure.Metrics
{
    public sealed class GitHubLinkMetricsRecorder : IGitHubLinkMetrics, IDisposable
    {
        private readonly Meter meter;
        private readonly Counter<long> successCounter;
        private readonly Counter<long> failureCounter;
        private bool disposed;

        public GitHubLinkMetricsRecorder()
        {
            meter = new Meter("MyApp.GitHubLink", "1.0.0");
            successCounter = meter.CreateCounter<long>("github_link_success_total");
            failureCounter = meter.CreateCounter<long>("github_link_failure_total");
            disposed = false;
        }

        public void RecordLinkSuccess(Guid userId)
        {
            successCounter.Add(1, new KeyValuePair<string, object?>[]
            {
                new KeyValuePair<string, object?>("userId", userId.ToString())
            });
        }

        public void RecordLinkFailure(Guid userId, string reason)
        {
            failureCounter.Add(1, new KeyValuePair<string, object?>[]
            {
                new KeyValuePair<string, object?>("userId", userId.ToString()),
                new KeyValuePair<string, object?>("reason", reason)
            });
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            meter.Dispose();
            disposed = true;
        }
    }
}
