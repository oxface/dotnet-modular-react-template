using Microsoft.EntityFrameworkCore;
using ModularTemplate.Identity.Infrastructure.Persistence;
using ModularTemplate.Identity.Infrastructure.Tests.Support;
using ModularTemplate.Infrastructure.Outbox;
using ModularTemplate.SharedKernel.Messaging;
using Shouldly;

namespace ModularTemplate.Identity.Infrastructure.Tests.Persistence;

public sealed class MessagePersistenceTests(PostgreSqlFixture postgreSqlFixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task SaveChangesAsync_WhenOutboxMessageIsAdded_PersistsRow()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);
        Guid messageId = Guid.NewGuid();

        dbContext.OutboxMessages.Add(OutboxMessage.Create(
            messageId,
            MessageKind.Event,
            "identity.local-user-created.v1",
            "identity",
            targetModule: null,
            correlationId: Guid.NewGuid(),
            causationId: null,
            operationId: null,
            payload: "{\"localUserId\":\"00000000-0000-0000-0000-000000000001\"}"));

        await dbContext.SaveChangesAsync(CancellationToken.None);

        OutboxMessage storedOutbox = await dbContext.OutboxMessages.SingleAsync(CancellationToken.None);
        storedOutbox.MessageId.ShouldBe(messageId);
        storedOutbox.Status.ShouldBe(PersistedMessageStatus.Pending);
        storedOutbox.MessageType.ShouldBe("identity.local-user-created.v1");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SaveChangesAsync_WhenOutboxMessageIdIsDuplicated_ThrowsDbUpdateException()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);
        Guid duplicateMessageId = Guid.NewGuid();

        dbContext.OutboxMessages.Add(CreateOutboxMessage(duplicateMessageId));
        await dbContext.SaveChangesAsync(CancellationToken.None);

        dbContext.OutboxMessages.Add(CreateOutboxMessage(duplicateMessageId));

        await Should.ThrowAsync<DbUpdateException>(
            async () => await dbContext.SaveChangesAsync(CancellationToken.None));
    }

    private IdentityDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(postgreSqlFixture.ConnectionString)
            .Options;

        return new IdentityDbContext(options);
    }

    private static OutboxMessage CreateOutboxMessage(Guid messageId)
    {
        return OutboxMessage.Create(
            messageId,
            MessageKind.Command,
            "identity.grant-access.v1",
            sourceModule: "operations",
            targetModule: "identity",
            correlationId: Guid.NewGuid(),
            causationId: null,
            operationId: null,
            payload: "{\"localUserId\":\"00000000-0000-0000-0000-000000000001\"}");
    }
}
