using Microsoft.EntityFrameworkCore;
using Bondstone.EntityFrameworkCore.Inbox;
using ModularTemplate.Identity.Infrastructure.Persistence;
using ModularTemplate.Identity.Infrastructure.Tests.Support;
using Bondstone.EntityFrameworkCore.Outbox;
using Bondstone.EntityFrameworkCore.Postgres.Inbox;
using Bondstone.Messaging;
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

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ClaimAsync_WhenInboxMessageIsMissing_CreatesReceiveClaim()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);
        var processor = CreateInboxMessageProcessor();

        InboxMessage? inboxMessage = await processor.ClaimAsync(
            dbContext,
            "message-1",
            "identity",
            "TestHandler",
            CancellationToken.None);

        inboxMessage.ShouldNotBeNull();
        inboxMessage.IsProcessed.ShouldBeFalse();
        InboxMessage storedInboxMessage = await dbContext.InboxMessages.SingleAsync(CancellationToken.None);
        storedInboxMessage.MessageId.ShouldBe("message-1");
        storedInboxMessage.ModuleName.ShouldBe("identity");
        storedInboxMessage.HandlerName.ShouldBe("TestHandler");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ClaimAsync_WhenInboxMessageIsAlreadyProcessed_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);
        InboxMessage inboxMessage = InboxMessage.Create("message-1", "identity", "TestHandler");
        inboxMessage.MarkProcessed();
        dbContext.InboxMessages.Add(inboxMessage);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var processor = CreateInboxMessageProcessor();

        InboxMessage? claimedInboxMessage = await processor.ClaimAsync(
            dbContext,
            "message-1",
            "identity",
            "TestHandler",
            CancellationToken.None);

        claimedInboxMessage.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ClaimAsync_WhenSameMessageIsClaimedConcurrently_ReturnsSingleProcessedClaim()
    {
        await using var firstContext = CreateDbContext();
        await firstContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await firstContext.Database.EnsureCreatedAsync(CancellationToken.None);
        await using var secondContext = CreateDbContext();
        await using var transaction = await firstContext.Database.BeginTransactionAsync(CancellationToken.None);
        var processor = CreateInboxMessageProcessor();

        InboxMessage? firstClaim = await processor.ClaimAsync(
            firstContext,
            "message-1",
            "identity",
            "TestHandler",
            CancellationToken.None);
        firstClaim.ShouldNotBeNull();
        firstClaim.MarkProcessed();
        await firstContext.SaveChangesAsync(CancellationToken.None);

        Task<InboxMessage?> duplicateClaimTask = processor.ClaimAsync(
            secondContext,
            "message-1",
            "identity",
            "TestHandler",
            CancellationToken.None);

        await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);

        Task completedTask = await Task.WhenAny(
            duplicateClaimTask,
            Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None));
        completedTask.ShouldBe(duplicateClaimTask);
        InboxMessage? duplicateClaim = await duplicateClaimTask;

        duplicateClaim.ShouldBeNull();
        (await firstContext.InboxMessages.CountAsync(CancellationToken.None)).ShouldBe(1);
        (await firstContext.InboxMessages.SingleAsync(CancellationToken.None)).IsProcessed.ShouldBeTrue();
    }

    private IdentityDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(postgreSqlFixture.ConnectionString)
            .Options;

        return new IdentityDbContext(options);
    }

    private static InboxMessageProcessor CreateInboxMessageProcessor()
    {
        return new InboxMessageProcessor(new PostgresInboxClaimConflictDetector());
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
