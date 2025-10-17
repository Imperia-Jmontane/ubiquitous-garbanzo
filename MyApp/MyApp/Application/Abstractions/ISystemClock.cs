using System;

namespace MyApp.Application.Abstractions
{
    public interface ISystemClock
    {
        DateTimeOffset UtcNow { get; }
    }
}
