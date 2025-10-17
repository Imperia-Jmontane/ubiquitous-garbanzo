using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyApp.Application.Authentication.Interfaces;
using MyApp.Domain.Entities;

namespace MyApp.Infrastructure.Persistence.Repositories
{
    public sealed class UserExternalLoginRepository : IUserExternalLoginRepository
    {
        private readonly ApplicationDbContext dbContext;

        public UserExternalLoginRepository(ApplicationDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        public async Task AddAsync(UserExternalLogin login, CancellationToken cancellationToken)
        {
            await dbContext.UserExternalLogins.AddAsync(login, cancellationToken);
        }

        public async Task<UserExternalLogin?> FindByStateAsync(string state, CancellationToken cancellationToken)
        {
            return await dbContext.UserExternalLogins.FirstOrDefaultAsync(login => login.State == state, cancellationToken);
        }

        public async Task<UserExternalLogin?> FindByUserAsync(string provider, string providerAccountId, CancellationToken cancellationToken)
        {
            return await dbContext.UserExternalLogins.FirstOrDefaultAsync(login => login.Provider == provider && login.ProviderAccountId == providerAccountId, cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
