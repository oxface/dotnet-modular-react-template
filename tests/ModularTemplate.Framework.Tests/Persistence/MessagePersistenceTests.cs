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
    public async Task SaveChangesAsync_WhenOutboxAndInboxMessagesAreAdded_PersistsRows()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);
        Guid messageId = Guid.NewGuid();
        var outboxMessage = OutboxMessage.Create(
            messageId,
            MessageKind.Event,
            "ModularTemplate.identity.local-user-created.v1",
            "identity",
            targetModule: null,
            correlationId: Guid.NewGuid(),
            causationId: null,
            operationId: null,
            payload: "{\"localUserId\":\"00000000-0000-0000-0000-000000000001\"}");
        var inboxMessage = InboxMessage.Create(
            Guid.NewGuid(),
            MessageKind.Command,
            "ModularTemplate.identity.grant-access.v1",
            sourceModule: "operations",
            targetModule: "identity",
            correlationId: Guid.NewGuid(),
            causationId: null,
            operationId: null,
            idempotencyKey: "grant-initial-admin",
            payload: "{\"localUserId\":\"00000000-0000-0000-0000-000000000001\"}");

        dbContext.OutboxMessages.Add(outboxMessage);
        dbContext.InboxMessages.Add(inboxMessage);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        OutboxMessage storedOutbox = await dbContext.OutboxMessages.SingleAsync(CancellationToken.None);
        InboxMessage storedInbox = await dbContext.InboxMessages.SingleAsync(CancellationToken.None);
        storedOutbox.MessageId.ShouldBe(messageId);
        storedOutbox.Status.ShouldBe(PersistedMessageStatus.Pending);
        storedInbox.TargetModule.ShouldBe("identity");
        storedInbox.Status.ShouldBe(PersistedMessageStatus.Pending);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SaveChangesAsync_WhenInboxMessageIdIsDuplicated_ThrowsDbUpdateException()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);
        Guid duplicateMessageId = Guid.NewGuid();

        dbContext.InboxMessages.Add(CreateInboxMessage(duplicateMessageId));
        await dbContext.SaveChangesAsync(CancellationToken.None);

        dbContext.InboxMessages.Add(CreateInboxMessage(duplicateMessageId));

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

    private static InboxMessage CreateInboxMessage(Guid messageId)
    {
        return InboxMessage.Create(
            messageId,
            MessageKind.Command,
            "ModularTemplate.identity.grant-access.v1",
            sourceModule: "operations",
            targetModule: "identity",
            correlationId: Guid.NewGuid(),
            causationId: null,
            operationId: null,
            idempotencyKey: null,
            payload: "{\"localUserId\":\"00000000-0000-0000-0000-000000000001\"}");
    }
}
