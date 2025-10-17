using System;

namespace MyApp.Application.Common.Interfaces
{
    public interface IDateTimeProvider
    {
        DateTimeOffset UtcNow { get; }
    }
}
