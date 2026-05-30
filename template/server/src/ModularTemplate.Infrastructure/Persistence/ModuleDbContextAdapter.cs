using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ModularTemplate.Infrastructure.Inbox;
using ModularTemplate.Infrastructure.Outbox;
using ModularTemplate.Infrastructure.Persistence.DomainEvents;

namespace ModularTemplate.Infrastructure.Persistence;

internal sealed class ModuleDbContextAdapter<TDbContext>(TDbContext context) : IModuleDbContext
    where TDbContext : DbContext, IModuleDbContext
{
    public string ModuleName => context.ModuleName;

    public DbSet<OutboxMessage> OutboxMessages => context.OutboxMessages;

    public DbSet<InboxMessage> InboxMessages => context.InboxMessages;

    public DbSet<StoredDomainEvent> DomainEvents => context.DomainEvents;

    public DatabaseFacade Database => context.Database;

    public ChangeTracker ChangeTracker => context.ChangeTracker;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return context.SaveChangesAsync(cancellationToken);
    }
}
