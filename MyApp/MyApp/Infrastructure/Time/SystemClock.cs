using System;
using MyApp.Application.Abstractions;

namespace MyApp.Infrastructure.Time
{
    public sealed class SystemClock : ISystemClock
    {
        public DateTimeOffset UtcNow
        {
            get { return DateTimeOffset.UtcNow; }
        }
    }
}
