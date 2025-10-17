using System;
using MyApp.Application.Common.Interfaces;

namespace MyApp.Infrastructure.Services
{
    public sealed class SystemDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
