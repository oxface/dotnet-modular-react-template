using Microsoft.EntityFrameworkCore;
using ModularTemplate.Identity.Users;

namespace ModularTemplate.Identity.Infrastructure.Persistence;

public sealed class LocalUserRepository(IdentityDbContext dbContext) : ILocalUserRepository
{
    public Task<LocalUser?> GetByProviderSubjectAsync(
        string provider,
        string subject,
        CancellationToken cancellationToken)
    {
        return dbContext.LocalUsers
            .SingleOrDefaultAsync(
                x => x.Provider == provider && x.Subject == subject,
                cancellationToken);
    }

    public void Add(LocalUser user)
    {
        dbContext.LocalUsers.Add(user);
    }
}
