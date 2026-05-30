using Microsoft.EntityFrameworkCore;
using ModularTemplate.Operations.Operations;
using ModularTemplate.Infrastructure.Inbox;
using ModularTemplate.Infrastructure.Outbox;
using ModularTemplate.Infrastructure.Persistence;
using ModularTemplate.Infrastructure.Persistence.DomainEvents;

namespace ModularTemplate.Operations.Infrastructure.Persistence;

public sealed class OperationsDbContext(DbContextOptions<OperationsDbContext> options)
    : DbContext(options), IModuleDbContext
{
    public DbSet<Operation> Operations => Set<Operation>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public DbSet<StoredDomainEvent> DomainEvents => Set<StoredDomainEvent>();

    string IModuleDbContext.ModuleName => "operations";

    DbSet<OutboxMessage> IModuleDbContext.OutboxMessages => OutboxMessages;

    DbSet<InboxMessage> IModuleDbContext.InboxMessages => InboxMessages;

    DbSet<StoredDomainEvent> IModuleDbContext.DomainEvents => DomainEvents;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("operations");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OperationsDbContext).Assembly);
        modelBuilder.ApplyOutboxConfiguration("operations");
    }
}
