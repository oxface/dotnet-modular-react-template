using Microsoft.EntityFrameworkCore;
using ModularTemplate.Identity.Access;
using ModularTemplate.Identity.Users;
using ModularTemplate.Infrastructure.Inbox;
using ModularTemplate.Infrastructure.Outbox;
using ModularTemplate.Infrastructure.Persistence;
using ModularTemplate.Infrastructure.Persistence.DomainEvents;

namespace ModularTemplate.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : DbContext(options), IModuleDbContext
{
    public DbSet<LocalUser> LocalUsers => Set<LocalUser>();

    public DbSet<ApplicationAccess> ApplicationAccess => Set<ApplicationAccess>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public DbSet<StoredDomainEvent> DomainEvents => Set<StoredDomainEvent>();

    string IModuleDbContext.ModuleName => "identity";

    DbSet<OutboxMessage> IModuleDbContext.OutboxMessages => OutboxMessages;

    DbSet<InboxMessage> IModuleDbContext.InboxMessages => InboxMessages;

    DbSet<StoredDomainEvent> IModuleDbContext.DomainEvents => DomainEvents;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        modelBuilder.ApplyOutboxConfiguration("identity");
    }
}
