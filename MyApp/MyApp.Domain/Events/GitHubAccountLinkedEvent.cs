using System;
using MyApp.Domain.Entities;

namespace MyApp.Domain.Events
{
    public sealed class GitHubAccountLinkedEvent
    {
        public GitHubAccountLinkedEvent(GitHubAccountLink accountLink)
        {
            AccountLink = accountLink ?? throw new ArgumentNullException(nameof(accountLink));
        }

        public GitHubAccountLink AccountLink { get; }
    }
}
