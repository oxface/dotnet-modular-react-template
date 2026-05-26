using Microsoft.EntityFrameworkCore;

namespace ModularTemplate.Outbox;

public static class OutboxModelBuilderExtensions
{
    public static ModelBuilder ApplyOutboxConfiguration(this ModelBuilder modelBuilder, string schema)
    {
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration(schema));
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration(schema));
        modelBuilder.ApplyConfiguration(new DomainEvents.StoredDomainEventConfiguration(schema));
        return modelBuilder;
    }
}
