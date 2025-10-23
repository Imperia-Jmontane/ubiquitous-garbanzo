using System.Threading;
using System.Threading.Tasks;

namespace MyApp.Application.Abstractions
{
    public interface ISecretProvider
    {
        Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken);
    }
}
