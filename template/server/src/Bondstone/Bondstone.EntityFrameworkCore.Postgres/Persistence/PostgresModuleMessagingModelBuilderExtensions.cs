using Microsoft.EntityFrameworkCore;
using Bondstone.EntityFrameworkCore.Outbox;
using Bondstone.EntityFrameworkCore.Persistence;
using Bondstone.EntityFrameworkCore.Persistence.DomainEvents;

namespace Bondstone.EntityFrameworkCore.Postgres.Persistence;

public static class PostgresModuleMessagingModelBuilderExtensions
{
    private const string PostgresDurablePayloadColumnType = "jsonb";

    public static ModelBuilder ApplyPostgresModuleMessagingPersistence(
        this ModelBuilder modelBuilder,
        string schema)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyModuleMessagingPersistence(schema);
        modelBuilder.ApplyPostgresDurablePayloadColumnTypes();

        return modelBuilder;
    }

    private static void ApplyPostgresDurablePayloadColumnTypes(this ModelBuilder modelBuilder)
    {
        // Provider-specific durable payload storage belongs here. PostgreSQL uses jsonb;
        // future providers can map these flexible payload columns to their native JSON or text type.
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.Property(x => x.Payload).HasColumnType(PostgresDurablePayloadColumnType);
            builder.Property(x => x.Metadata).HasColumnType(PostgresDurablePayloadColumnType);
        });

        modelBuilder.Entity<StoredDomainEvent>(builder =>
        {
            builder.Property(x => x.Payload).HasColumnType(PostgresDurablePayloadColumnType);
            builder.Property(x => x.Metadata).HasColumnType(PostgresDurablePayloadColumnType);
        });
    }
}
