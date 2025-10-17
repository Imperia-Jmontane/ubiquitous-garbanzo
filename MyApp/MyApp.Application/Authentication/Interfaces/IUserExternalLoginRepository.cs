using System.Threading;
using System.Threading.Tasks;
using MyApp.Domain.Entities;

namespace MyApp.Application.Authentication.Interfaces
{
    public interface IUserExternalLoginRepository
    {
        Task AddAsync(UserExternalLogin login, CancellationToken cancellationToken);

        Task<UserExternalLogin?> FindByStateAsync(string state, CancellationToken cancellationToken);

        Task<UserExternalLogin?> FindByUserAsync(string provider, string providerAccountId, CancellationToken cancellationToken);

        Task SaveChangesAsync(CancellationToken cancellationToken);
    }
}
