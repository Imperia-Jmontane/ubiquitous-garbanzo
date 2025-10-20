using System;

namespace MyApp.Application.Abstractions
{
    public enum RepositoryCloneState
    {
        Queued = 0,
        Running = 1,
        Completed = 2,
        Failed = 3,
        Canceled = 4
    }
}
