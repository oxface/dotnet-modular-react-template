using Microsoft.EntityFrameworkCore;
using Bondstone.EntityFrameworkCore.Inbox;
using Bondstone.EntityFrameworkCore.Outbox;
using Bondstone.EntityFrameworkCore.Persistence.DomainEvents;

namespace Bondstone.EntityFrameworkCore.Persistence;

public static class ModuleMessagingModelBuilderExtensions
{
    public static ModelBuilder ApplyModuleMessagingPersistence(this ModelBuilder modelBuilder, string schema)
    {
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration(schema));
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration(schema));
        modelBuilder.ApplyConfiguration(new StoredDomainEventConfiguration(schema));
        return modelBuilder;
    }
}
