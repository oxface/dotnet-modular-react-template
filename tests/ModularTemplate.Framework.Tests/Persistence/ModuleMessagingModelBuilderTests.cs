using Microsoft.EntityFrameworkCore;
using Bondstone.EntityFrameworkCore.Inbox;
using Bondstone.EntityFrameworkCore.Outbox;
using Bondstone.EntityFrameworkCore.Persistence;
using Bondstone.EntityFrameworkCore.Persistence.DomainEvents;
using Bondstone.EntityFrameworkCore.Postgres.Persistence;
using Shouldly;

namespace ModularTemplate.Identity.Infrastructure.Tests.Persistence;

public sealed class ModuleMessagingModelBuilderTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ApplyPostgresModuleMessagingPersistence_MapsDurablePayloadStorageToJsonb()
    {
        using var dbContext = CreateDbContext();

        AssertColumnType<OutboxMessage>(dbContext, nameof(OutboxMessage.Payload), "jsonb");
        AssertColumnType<OutboxMessage>(dbContext, nameof(OutboxMessage.Metadata), "jsonb");
        AssertColumnType<StoredDomainEvent>(dbContext, nameof(StoredDomainEvent.Payload), "jsonb");
        AssertColumnType<StoredDomainEvent>(dbContext, nameof(StoredDomainEvent.Metadata), "jsonb");
    }

    private static MessagingModelDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MessagingModelDbContext>()
            .UseNpgsql("Host=localhost;Database=messaging_model_tests;Username=postgres;Password=postgres")
            .Options;

        return new MessagingModelDbContext(options);
    }

    private static void AssertColumnType<TEntity>(
        DbContext dbContext,
        string propertyName,
        string expectedColumnType)
    {
        dbContext.Model
            .FindEntityType(typeof(TEntity))
            .ShouldNotBeNull()
            .FindProperty(propertyName)
            .ShouldNotBeNull()
            .GetColumnType()
            .ShouldBe(expectedColumnType);
    }

    private sealed class MessagingModelDbContext(DbContextOptions<MessagingModelDbContext> options)
        : ModuleDbContext<MessagingModelDbContext>(options)
    {
        public override string ModuleName => "messaging_model";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(ModuleName);
            modelBuilder.ApplyPostgresModuleMessagingPersistence(ModuleName);
        }
    }
}
