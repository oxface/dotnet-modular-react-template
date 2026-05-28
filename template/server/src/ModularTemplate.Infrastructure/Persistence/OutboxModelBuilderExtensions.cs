using Microsoft.EntityFrameworkCore;
using ModularTemplate.Infrastructure.Outbox;
using ModularTemplate.Infrastructure.Persistence.DomainEvents;

namespace ModularTemplate.Infrastructure.Persistence;

public static class OutboxModelBuilderExtensions
{
    public static ModelBuilder ApplyOutboxConfiguration(this ModelBuilder modelBuilder, string schema)
    {
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration(schema));
        modelBuilder.ApplyConfiguration(new StoredDomainEventConfiguration(schema));
        return modelBuilder;
    }
}
