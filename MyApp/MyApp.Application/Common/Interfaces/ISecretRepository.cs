using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyApp.Application.Common.Interfaces
{
    public interface ISecretRepository
    {
        Task<string> SetSecretAsync(string name, string value, IDictionary<string, string> tags, CancellationToken cancellationToken);

        Task UpdateSecretAsync(string name, string value, IDictionary<string, string> tags, CancellationToken cancellationToken);

        Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken);
    }
}
