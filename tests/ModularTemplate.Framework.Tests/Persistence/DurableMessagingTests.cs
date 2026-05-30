using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModularTemplate.Identity.Infrastructure.Persistence;
using ModularTemplate.Identity.Infrastructure.Tests.Support;
using ModularTemplate.Infrastructure.Inbox;
using ModularTemplate.Infrastructure.Outbox;
using ModularTemplate.Infrastructure.Persistence;
using ModularTemplate.Infrastructure.Persistence.DomainEvents;
using ModularTemplate.SharedKernel.Messaging;
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
            operationId: null,
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
    public async Task DispatchPendingAsync_WhenOneModuleFails_ContinuesWithNextModule()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);
        Guid messageId = Guid.NewGuid();
        dbContext.OutboxMessages.Add(CreateCommand(messageId));
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var transport = new TestOutboxTransport();
        var dispatcher = CreateDispatcher(
            [new UnavailableModuleDbContext("broken"), (IModuleDbContext)dbContext],
            transport,
            new FailingModuleOutboxDispatchLock("broken"));

        int dispatchedCount = await dispatcher.DispatchPendingAsync(CancellationToken.None);

        dispatchedCount.ShouldBe(1);
        transport.DispatchedMessageIds.ShouldBe([messageId]);
        OutboxMessage outbox = await dbContext.OutboxMessages.SingleAsync(CancellationToken.None);
        outbox.Status.ShouldBe(PersistedMessageStatus.Processed);
    }

    private IdentityDbContext CreateDbContext()
    {
        DbContextOptions<IdentityDbContext> options =
            new DbContextOptionsBuilder<IdentityDbContext>()
                .UseNpgsql(postgreSqlFixture.ConnectionString)
                .Options;

        return new IdentityDbContext(options);
    }

    private static OutboxDispatcher CreateDispatcher(
        IdentityDbContext dbContext,
        IOutboxTransport transport)
    {
        return CreateDispatcher(
            [(IModuleDbContext)dbContext],
            transport,
            new AlwaysAcquiredOutboxDispatchLock());
    }

    private static OutboxDispatcher CreateDispatcher(
        IEnumerable<IModuleDbContext> dbContexts,
        IOutboxTransport transport,
        IOutboxDispatchLock outboxDispatchLock)
    {
        var options = Options.Create(new DurableMessagingOptions
        {
            BatchSize = 20,
            MaxAttempts = 5,
            RetryDelays = [TimeSpan.FromSeconds(10)]
        });

        return new OutboxDispatcher(
            dbContexts,
            transport,
            outboxDispatchLock,
            options,
            new ConfiguredRetryDelayPolicy(options),
            NullLogger<OutboxDispatcher>.Instance);
    }

    private static OutboxMessage CreateCommand(Guid messageId)
    {
        return OutboxMessage.Create(
            messageId,
            MessageKind.Command,
            "operations.rebuild-projection.v1",
            sourceModule: "identity",
            targetModule: "operations",
            correlationId: Guid.NewGuid(),
            causationId: null,
            operationId: null,
            payload: "{}");
    }

    private sealed class TestOutboxTransport : IOutboxTransport
    {
        public List<Guid> DispatchedMessageIds { get; } = [];

        public bool ThrowOnDispatch { get; init; }

        public Task DispatchAsync(OutboxMessage outboxMessage, CancellationToken cancellationToken)
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

    private sealed class FailingModuleOutboxDispatchLock(string failingModuleName) : IOutboxDispatchLock
    {
        public Task<IAsyncDisposable?> TryAcquireAsync(
            IModuleDbContext dbContext,
            CancellationToken cancellationToken)
        {
            if (string.Equals(dbContext.ModuleName, failingModuleName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("module dispatch unavailable");
            }

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

    private sealed class UnavailableModuleDbContext(string moduleName) : IModuleDbContext
    {
        public string ModuleName { get; } = moduleName;

        public DbSet<OutboxMessage> OutboxMessages => throw new NotSupportedException();

        public DbSet<InboxMessage> InboxMessages => throw new NotSupportedException();

        public DbSet<StoredDomainEvent> DomainEvents => throw new NotSupportedException();

        public DatabaseFacade Database => throw new NotSupportedException();

        public ChangeTracker ChangeTracker => throw new NotSupportedException();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
