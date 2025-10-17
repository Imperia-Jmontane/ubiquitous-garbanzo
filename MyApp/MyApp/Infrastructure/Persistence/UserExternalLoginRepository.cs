using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyApp.Application.Abstractions;
using MyApp.Data;
using MyApp.Domain.Identity;

namespace MyApp.Infrastructure.Persistence
{
    public sealed class UserExternalLoginRepository : IUserExternalLoginRepository
    {
        private readonly ApplicationDbContext dbContext;

        public UserExternalLoginRepository(ApplicationDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        public async Task<UserExternalLogin?> GetAsync(Guid userId, string provider, CancellationToken cancellationToken)
        {
            return await dbContext.UserExternalLogins.SingleOrDefaultAsync(login => login.UserId == userId && login.Provider == provider, cancellationToken);
        }

        public async Task AddAsync(UserExternalLogin login, CancellationToken cancellationToken)
        {
            await dbContext.UserExternalLogins.AddAsync(login, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateAsync(UserExternalLogin login, CancellationToken cancellationToken)
        {
            dbContext.UserExternalLogins.Update(login);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
