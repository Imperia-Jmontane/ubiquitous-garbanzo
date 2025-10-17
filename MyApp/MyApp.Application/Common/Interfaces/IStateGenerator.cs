using System;

namespace MyApp.Application.Common.Interfaces
{
    public interface IStateGenerator
    {
        string CreateState(Guid userId);
    }
}
