using Microsoft.EntityFrameworkCore;
using Bondstone.EntityFrameworkCore.Inbox;
using Bondstone.EntityFrameworkCore.Outbox;
using Bondstone.EntityFrameworkCore.Persistence.DomainEvents;

namespace Bondstone.EntityFrameworkCore.Persistence;

public abstract class ModuleDbContext<TDbContext>(DbContextOptions<TDbContext> options)
    : DbContext(options), IModuleDbContext
    where TDbContext : DbContext
{
    public abstract string ModuleName { get; }

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public DbSet<StoredDomainEvent> DomainEvents => Set<StoredDomainEvent>();

    protected void ApplyModuleMessagingPersistence(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyModuleMessagingPersistence(ModuleName);
    }
}
