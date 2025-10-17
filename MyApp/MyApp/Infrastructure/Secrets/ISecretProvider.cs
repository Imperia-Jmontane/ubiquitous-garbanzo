using System.Threading;
using System.Threading.Tasks;

namespace MyApp.Infrastructure.Secrets
{
    public interface ISecretProvider
    {
        Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken);
    }
}
