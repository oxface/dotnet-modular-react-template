using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ModularTemplate.Infrastructure.Outbox;
using ModularTemplate.Infrastructure.Persistence.DomainEvents;

namespace ModularTemplate.Infrastructure.Persistence;

/// <summary>
/// Marker interface implemented by each module's DbContext.
/// Exposes the outbox, inbox, and domain-event sets so that
/// OutboxDispatcher, InboxProcessor, and the module unit of work
/// can work against any module context without knowing its concrete type.
/// </summary>
public interface IModuleDbContext
{
    /// <summary>
    /// The module name, used as the PostgreSQL schema name (e.g. "identity", "operations").
    /// </summary>
    string ModuleName { get; }

    DbSet<OutboxMessage> OutboxMessages { get; }

    DbSet<InboxMessage> InboxMessages { get; }

    DbSet<StoredDomainEvent> DomainEvents { get; }

    DatabaseFacade Database { get; }

    ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
