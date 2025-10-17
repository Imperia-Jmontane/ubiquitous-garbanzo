using System;

namespace MyApp.Application.Common.Interfaces
{
    public interface IGitHubLinkMetrics
    {
        void RecordLinkSuccess(Guid userId);

        void RecordLinkFailure(Guid userId, string reason);
    }
}
