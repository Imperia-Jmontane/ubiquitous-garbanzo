using System;
using System.Threading;
using System.Threading.Tasks;
using MyApp.Domain.Identity;

namespace MyApp.Application.Abstractions
{
    public interface IUserExternalLoginRepository
    {
        Task<UserExternalLogin?> GetAsync(Guid userId, string provider, CancellationToken cancellationToken);

        Task AddAsync(UserExternalLogin login, CancellationToken cancellationToken);

        Task UpdateAsync(UserExternalLogin login, CancellationToken cancellationToken);
    }
}
