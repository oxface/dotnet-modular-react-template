using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModularTemplate.Identity.Infrastructure.Persistence;
using ModularTemplate.Identity.Infrastructure.Tests.Support;
using Bondstone.EntityFrameworkCore.Outbox;
using Bondstone.EntityFrameworkCore.Persistence;
using Bondstone.EntityFrameworkCore.Postgres.Outbox;
using Bondstone.Messaging;
using Shouldly;

namespace ModularTemplate.Identity.Infrastructure.Tests.Persistence;

public sealed class DurableMessagingTests(PostgreSqlFixture postgreSqlFixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task DispatchPendingAsync_WhenCommandOutboxMessageExists_DispatchesAndMarksOutboxProcessed()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);
        Guid messageId = Guid.NewGuid();
        dbContext.OutboxMessages.Add(CreateCommand(messageId));
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var transport = new TestOutboxTransport();
        var dispatcher = CreateDispatcher(dbContext, transport);

        int dispatchedCount = await dispatcher.DispatchPendingAsync(CancellationToken.None);

        dispatchedCount.ShouldBe(1);
        transport.DispatchedMessageIds.ShouldBe([messageId]);
        OutboxMessage outbox = await dbContext.OutboxMessages.SingleAsync(CancellationToken.None);
        outbox.Status.ShouldBe(PersistedMessageStatus.Processed);
        outbox.DispatchedAtUtc.ShouldNotBeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DispatchPendingAsync_WhenEventOutboxMessageExists_DispatchesAndMarksOutboxProcessed()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);
        Guid messageId = Guid.NewGuid();
        dbContext.OutboxMessages.Add(OutboxMessage.Create(
            messageId,
            MessageKind.Event,
            "identity.local-user-created.v1",
            sourceModule: "identity",
            targetModule: null,
            correlationId: Guid.NewGuid(),
            causationId: null,
            durableOperationId: null,
            payload: "{}"));
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var transport = new TestOutboxTransport();
        var dispatcher = CreateDispatcher(dbContext, transport);

        int dispatchedCount = await dispatcher.DispatchPendingAsync(CancellationToken.None);

        dispatchedCount.ShouldBe(1);
        transport.DispatchedMessageIds.ShouldBe([messageId]);
        OutboxMessage outbox = await dbContext.OutboxMessages.SingleAsync(CancellationToken.None);
        outbox.Status.ShouldBe(PersistedMessageStatus.Processed);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DispatchPendingAsync_WhenTransportFails_MarksOutboxFailedWithRetry()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);
        dbContext.OutboxMessages.Add(CreateCommand(Guid.NewGuid()));
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var transport = new TestOutboxTransport { ThrowOnDispatch = true };
        var dispatcher = CreateDispatcher(dbContext, transport);

        int dispatchedCount = await dispatcher.DispatchPendingAsync(CancellationToken.None);

        dispatchedCount.ShouldBe(0);
        OutboxMessage outbox = await dbContext.OutboxMessages.SingleAsync(CancellationToken.None);
        outbox.Status.ShouldBe(PersistedMessageStatus.Failed);
        outbox.AttemptCount.ShouldBe(1);
        outbox.NextAttemptAtUtc.ShouldBeGreaterThanOrEqualTo(DateTimeOffset.UtcNow.AddSeconds(9));
        outbox.Error.ShouldNotBeNull();
        outbox.Error.ShouldContain("transport failed");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DispatchPendingAsync_WhenProcessingMessageIsFresh_DoesNotReclaimMessage()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);
        Guid messageId = Guid.NewGuid();
        dbContext.OutboxMessages.Add(CreateCommand(messageId));
        await dbContext.SaveChangesAsync(CancellationToken.None);
        await MarkProcessingAsync(dbContext, messageId, DateTimeOffset.UtcNow);
        var transport = new TestOutboxTransport();
        var dispatcher = CreateDispatcher(dbContext, transport);

        int dispatchedCount = await dispatcher.DispatchPendingAsync(CancellationToken.None);

        dispatchedCount.ShouldBe(0);
        transport.DispatchedMessageIds.ShouldBeEmpty();
        OutboxMessage outbox = await dbContext.OutboxMessages.SingleAsync(CancellationToken.None);
        outbox.Status.ShouldBe(PersistedMessageStatus.Processing);
        outbox.LockedBy.ShouldBe("existing-dispatcher");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DispatchPendingAsync_WhenProcessingMessageIsStale_ReclaimsAndDispatchesMessage()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);
        Guid messageId = Guid.NewGuid();
        dbContext.OutboxMessages.Add(CreateCommand(messageId));
        await dbContext.SaveChangesAsync(CancellationToken.None);
        await MarkProcessingAsync(dbContext, messageId, DateTimeOffset.UtcNow.AddMinutes(-5));
        var transport = new TestOutboxTransport();
        var dispatcher = CreateDispatcher(dbContext, transport);

        int dispatchedCount = await dispatcher.DispatchPendingAsync(CancellationToken.None);

        dispatchedCount.ShouldBe(1);
        transport.DispatchedMessageIds.ShouldBe([messageId]);
        OutboxMessage outbox = await dbContext.OutboxMessages.SingleAsync(CancellationToken.None);
        outbox.Status.ShouldBe(PersistedMessageStatus.Processed);
        outbox.AttemptCount.ShouldBe(1);
        outbox.LockedBy.ShouldBeNull();
        outbox.LockedAtUtc.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DispatchPendingAsync_WhenProcessingMessageIsStaleAtMaxAttempts_DeadLettersWithoutDispatching()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);
        Guid messageId = Guid.NewGuid();
        dbContext.OutboxMessages.Add(CreateCommand(messageId, maxAttempts: 1));
        await dbContext.SaveChangesAsync(CancellationToken.None);
        await MarkProcessingAsync(dbContext, messageId, DateTimeOffset.UtcNow.AddMinutes(-5));
        var transport = new TestOutboxTransport();
        var dispatcher = CreateDispatcher(dbContext, transport);

        int dispatchedCount = await dispatcher.DispatchPendingAsync(CancellationToken.None);

        dispatchedCount.ShouldBe(0);
        transport.DispatchedMessageIds.ShouldBeEmpty();
        OutboxMessage outbox = await dbContext.OutboxMessages.SingleAsync(CancellationToken.None);
        outbox.Status.ShouldBe(PersistedMessageStatus.DeadLettered);
        outbox.AttemptCount.ShouldBe(1);
        outbox.LockedBy.ShouldBeNull();
        outbox.LockedAtUtc.ShouldBeNull();
        outbox.Error.ShouldNotBeNull();
        outbox.Error.ShouldContain("lock timed out");
    }

    private IdentityDbContext CreateDbContext()
    {
        DbContextOptions<IdentityDbContext> options =
            new DbContextOptionsBuilder<IdentityDbContext>()
                .UseNpgsql(postgreSqlFixture.ConnectionString)
                .Options;

        return new IdentityDbContext(options);
    }

    private static async Task MarkProcessingAsync(
        IdentityDbContext dbContext,
        Guid messageId,
        DateTimeOffset lockedAtUtc)
    {
        await dbContext.Database.ExecuteSqlAsync(
            $"""
            UPDATE identity.outbox_messages
            SET "Status" = 'Processing',
                "LockedAtUtc" = {lockedAtUtc},
                "LockedBy" = 'existing-dispatcher'
            WHERE "MessageId" = {messageId}
            """);
        dbContext.ChangeTracker.Clear();
    }

    private static OutboxDispatcher<IdentityDbContext> CreateDispatcher(
        IdentityDbContext dbContext,
        IOutboxTransport transport)
    {
        return CreateDispatcher(
            dbContext,
            transport,
            new AlwaysAcquiredOutboxDispatchLock());
    }

    private static OutboxDispatcher<IdentityDbContext> CreateDispatcher(
        IdentityDbContext dbContext,
        IOutboxTransport transport,
        IOutboxDispatchLock outboxDispatchLock)
    {
        var options = Options.Create(new DurableMessagingOptions
        {
            BatchSize = 20,
            MaxAttempts = 5,
            RetryDelays = [TimeSpan.FromSeconds(10)]
        });

        return new OutboxDispatcher<IdentityDbContext>(
            dbContext,
            transport,
            outboxDispatchLock,
            new PostgresOutboxClaimHandler(),
            options,
            new ConfiguredRetryDelayPolicy(options),
            NullLogger<OutboxDispatcher<IdentityDbContext>>.Instance);
    }

    private static OutboxMessage CreateCommand(Guid messageId, int maxAttempts = 5)
    {
        return OutboxMessage.Create(
            messageId,
            MessageKind.Command,
            "products.rebuild-projection.v1",
            sourceModule: "identity",
            targetModule: "products",
            correlationId: Guid.NewGuid(),
            causationId: null,
            durableOperationId: null,
            payload: "{}",
            maxAttempts: maxAttempts);
    }

    private sealed class TestOutboxTransport : IOutboxTransport
    {
        public List<Guid> DispatchedMessageIds { get; } = [];

        public bool ThrowOnDispatch { get; init; }

        public Task DispatchAsync(IDurableOutboxMessage outboxMessage, CancellationToken cancellationToken)
        {
            if (ThrowOnDispatch)
            {
                throw new InvalidOperationException("transport failed");
            }

            DispatchedMessageIds.Add(outboxMessage.MessageId);
            return Task.CompletedTask;
        }
    }

    private sealed class AlwaysAcquiredOutboxDispatchLock : IOutboxDispatchLock
    {
        public Task<IAsyncDisposable?> TryAcquireAsync(
            IModuleDbContext dbContext,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IAsyncDisposable?>(new Lease());
        }

        private sealed class Lease : IAsyncDisposable
        {
            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }
    }

}
