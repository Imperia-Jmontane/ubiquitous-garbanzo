using System.Threading;
using System.Threading.Tasks;

namespace MyApp.Application.Abstractions
{
    public interface IWritableSecretStore
    {
        Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken);

        Task SetSecretAsync(string name, string value, CancellationToken cancellationToken);
    }
}
