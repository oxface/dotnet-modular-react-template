using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModularTemplate.Identity.Infrastructure.Tests.Support;
using ModularTemplate.Identity.Infrastructure.Persistence;
using ModularTemplate.Infrastructure.Outbox;
using ModularTemplate.Infrastructure.Persistence;
using ModularTemplate.Operations.Infrastructure.Persistence;
using ModularTemplate.SharedKernel.Messaging;
using Shouldly;

namespace ModularTemplate.Identity.Infrastructure.Tests.Persistence;

public sealed class DurableMessagingTests(PostgreSqlFixture postgreSqlFixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task DispatchPendingAsync_WhenCommandOutboxMessageExists_CreatesInboxMessageAndMarksOutboxProcessed()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);
        dbContext.OutboxMessages.Add(OutboxMessage.Create(
            messageId: Guid.NewGuid(),
            messageKind: MessageKind.Command,
            messageType: "ModularTemplate.notifications.send-email.v1",
            sourceModule: "notifications",
            targetModule: "operations",
            correlationId: Guid.NewGuid(),
            causationId: null,
            operationId: null,
            payload: "{\"subject\":\"hello\"}"));
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var dispatcher = new OutboxDispatcher(
            [dbContext],
            new TestOutboxTransport(dbContext),
            new LocalSubscriptionRegistry(),
            Options.Create(new DurableMessagingOptions { BatchSize = 20, MaxAttempts = 5 }),
            NullLogger<OutboxDispatcher>.Instance);

        int dispatchedCount = await dispatcher.DispatchPendingAsync(CancellationToken.None);

        dispatchedCount.ShouldBe(1);
        OutboxMessage outbox = await dbContext.OutboxMessages.SingleAsync(CancellationToken.None);
        InboxMessage inbox = await dbContext.InboxMessages.SingleAsync(CancellationToken.None);
        outbox.Status.ShouldBe(PersistedMessageStatus.Processed);
        inbox.Status.ShouldBe(PersistedMessageStatus.Pending);
        inbox.TargetModule.ShouldBe("operations");
        inbox.MessageType.ShouldBe("ModularTemplate.notifications.send-email.v1");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DispatchPendingAsync_WhenEventOutboxMessageHasNoTarget_MarksProcessedWithoutInbox()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);
        dbContext.OutboxMessages.Add(OutboxMessage.Create(
            messageId: Guid.NewGuid(),
            messageKind: MessageKind.Event,
            messageType: "ModularTemplate.notifications.message-sent.v1",
            sourceModule: "notifications",
            targetModule: null,
            correlationId: Guid.NewGuid(),
            causationId: null,
            operationId: null,
            payload: "{\"messageId\":\"abc\"}"));
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var dispatcher2 = new OutboxDispatcher(
            [(IModuleDbContext)dbContext],
            new TestOutboxTransport(dbContext),
            new LocalSubscriptionRegistry(),
            Options.Create(new DurableMessagingOptions { BatchSize = 20, MaxAttempts = 5 }),
            NullLogger<OutboxDispatcher>.Instance);

        int dispatchedCount2 = await dispatcher2.DispatchPendingAsync(CancellationToken.None);

        dispatchedCount2.ShouldBe(1);
        OutboxMessage outbox2 = await dbContext.OutboxMessages.SingleAsync(CancellationToken.None);
        outbox2.Status.ShouldBe(PersistedMessageStatus.Processed);
        (await dbContext.InboxMessages.CountAsync(CancellationToken.None)).ShouldBe(0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProcessPendingAsync_WhenHandlerSucceeds_MarksInboxProcessed()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);
        Guid messageId = Guid.NewGuid();
        var command = new TestDurableCommand("job-1");
        dbContext.InboxMessages.Add(InboxMessage.Create(
            messageId,
            MessageKind.Command,
            "ModularTemplate.operations.test-command.v1",
            sourceModule: "operations",
            targetModule: "operations",
            correlationId: Guid.NewGuid(),
            causationId: null,
            operationId: null,
            idempotencyKey: null,
            payload: JsonSerializer.Serialize(command)));
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var registry = new MessageTypeRegistry();
        registry.Register<TestDurableCommand>("ModularTemplate.operations.test-command.v1");
        var invocationTracker = new HandlerInvocationTracker();
        ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton(invocationTracker)
            .AddScoped<IDurableCommandHandler<TestDurableCommand>, TestDurableCommandHandler>()
            .BuildServiceProvider();

        try
        {
            IModuleDbContext iCtx = dbContext;
            var processor = new InboxProcessor(
                serviceProvider,
                [iCtx],
                registry,
                Options.Create(new DurableMessagingOptions { BatchSize = 20, MaxAttempts = 5 }),
                NullLogger<InboxProcessor>.Instance);

            int processedCount = await processor.ProcessPendingAsync(CancellationToken.None);

            processedCount.ShouldBe(1);
            invocationTracker.HandledJobIds.ShouldBe(["job-1"]);
            InboxMessage inbox = await dbContext.InboxMessages.SingleAsync(CancellationToken.None);
            inbox.Status.ShouldBe(PersistedMessageStatus.Processed);
            inbox.ProcessedAtUtc.ShouldNotBeNull();
        }
        finally
        {
            await serviceProvider.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProcessPendingAsync_WhenHandlerAlwaysFails_DeadLettersAfterMaxAttempts()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);
        dbContext.InboxMessages.Add(InboxMessage.Create(
            Guid.NewGuid(),
            MessageKind.Command,
            "ModularTemplate.operations.failing-command.v1",
            sourceModule: "operations",
            targetModule: "operations",
            correlationId: Guid.NewGuid(),
            causationId: null,
            operationId: null,
            idempotencyKey: null,
            payload: JsonSerializer.Serialize(new FailingCommand("job-fail")),
            maxAttempts: 2));
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var registry = new MessageTypeRegistry();
        registry.Register<FailingCommand>("ModularTemplate.operations.failing-command.v1");
        ServiceProvider serviceProvider = new ServiceCollection()
            .AddScoped<IDurableCommandHandler<FailingCommand>, AlwaysFailingCommandHandler>()
            .BuildServiceProvider();

        try
        {
            var processor = new InboxProcessor(
                serviceProvider,
                [(IModuleDbContext)dbContext],
                registry,
                Options.Create(new DurableMessagingOptions { BatchSize = 20, MaxAttempts = 5 }),
                NullLogger<InboxProcessor>.Instance);

            await processor.ProcessPendingAsync(CancellationToken.None);
            await processor.ProcessPendingAsync(CancellationToken.None);

            InboxMessage inbox = await dbContext.InboxMessages.SingleAsync(CancellationToken.None);
            inbox.AttemptCount.ShouldBe(2);
            inbox.Status.ShouldBe(PersistedMessageStatus.DeadLettered);
            inbox.Error.ShouldNotBeNull();
            inbox.Error.ShouldContain("Simulated processing failure");
        }
        finally
        {
            await serviceProvider.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DispatchPendingAsync_WhenEventHasRegisteredSubscribers_CreatesOneInboxMessagePerSubscriber()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);
        dbContext.OutboxMessages.Add(OutboxMessage.Create(
            messageId: Guid.NewGuid(),
            messageKind: MessageKind.Event,
            messageType: "ModularTemplate.identity.user-registered.v1",
            sourceModule: "identity",
            targetModule: null,
            correlationId: Guid.NewGuid(),
            causationId: null,
            operationId: null,
            payload: "{\"userId\":\"abc\"}"));
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var registry = new LocalSubscriptionRegistry();
        registry.RegisterEventSubscriber("ModularTemplate.identity.user-registered.v1", "notifications");
        registry.RegisterEventSubscriber("ModularTemplate.identity.user-registered.v1", "operations");

        var dispatcher = new OutboxDispatcher(
            [(IModuleDbContext)dbContext],
            new TestOutboxTransport(dbContext),
            registry,
            Options.Create(new DurableMessagingOptions { BatchSize = 20 }),
            NullLogger<OutboxDispatcher>.Instance);

        int dispatchedCount = await dispatcher.DispatchPendingAsync(CancellationToken.None);

        dispatchedCount.ShouldBe(1);
        OutboxMessage outbox = await dbContext.OutboxMessages.SingleAsync(CancellationToken.None);
        outbox.Status.ShouldBe(PersistedMessageStatus.Processed);
        List<InboxMessage> inboxRows = await dbContext.InboxMessages
            .OrderBy(x => x.TargetModule)
            .ToListAsync(CancellationToken.None);
        inboxRows.Count.ShouldBe(2);
        inboxRows.Select(x => x.TargetModule).ShouldBe(["notifications", "operations"]);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DispatchPendingAsync_WhenTransportAlwaysFails_DeadLettersOutboxAfterMaxAttempts()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);
        dbContext.OutboxMessages.Add(OutboxMessage.Create(
            messageId: Guid.NewGuid(),
            messageKind: MessageKind.Command,
            messageType: "ModularTemplate.ops.failing-dispatch.v1",
            sourceModule: "ops",
            targetModule: "ops",
            correlationId: Guid.NewGuid(),
            causationId: null,
            operationId: null,
            payload: "{}",
            maxAttempts: 2));
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var dispatcher = new OutboxDispatcher(
            [(IModuleDbContext)dbContext],
            new ThrowingOutboxTransport(),
            new LocalSubscriptionRegistry(),
            Options.Create(new DurableMessagingOptions { BatchSize = 20 }),
            NullLogger<OutboxDispatcher>.Instance);

        // Attempt 1: fails, Status → Failed.
        await dispatcher.DispatchPendingAsync(CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        OutboxMessage afterFirst = await dbContext.OutboxMessages.SingleAsync(CancellationToken.None);
        afterFirst.AttemptCount.ShouldBe(1);
        afterFirst.Status.ShouldBe(PersistedMessageStatus.Failed);

        // Attempt 2: exceeds maxAttempts → DeadLettered.
        await dispatcher.DispatchPendingAsync(CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        OutboxMessage afterSecond = await dbContext.OutboxMessages.SingleAsync(CancellationToken.None);
        afterSecond.AttemptCount.ShouldBe(2);
        afterSecond.Status.ShouldBe(PersistedMessageStatus.DeadLettered);
        afterSecond.Error.ShouldNotBeNull();
        afterSecond.Error.ShouldContain("Simulated transport failure");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task FullPipeline_WhenCommandIsQueued_OutboxDispatchedAndHandlerInvoked()
    {
        await using var dispatchContext = CreateDbContext();
        await dispatchContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await dispatchContext.Database.EnsureCreatedAsync(CancellationToken.None);
        var command = new TestDurableCommand("pipeline-job");
        dispatchContext.OutboxMessages.Add(OutboxMessage.Create(
            messageId: Guid.NewGuid(),
            messageKind: MessageKind.Command,
            messageType: "ModularTemplate.operations.test-command.v1",
            sourceModule: "operations",
            targetModule: "operations",
            correlationId: Guid.NewGuid(),
            causationId: null,
            operationId: null,
            payload: JsonSerializer.Serialize(command)));
        await dispatchContext.SaveChangesAsync(CancellationToken.None);

        // Stage 1: outbox → inbox via TestOutboxTransport.
        var dispatcher = new OutboxDispatcher(
            [(IModuleDbContext)dispatchContext],
            new TestOutboxTransport(dispatchContext),
            new LocalSubscriptionRegistry(),
            Options.Create(new DurableMessagingOptions { BatchSize = 20 }),
            NullLogger<OutboxDispatcher>.Instance);
        await dispatcher.DispatchPendingAsync(CancellationToken.None);
        (await dispatchContext.OutboxMessages.SingleAsync(CancellationToken.None))
            .Status.ShouldBe(PersistedMessageStatus.Processed);

        // Stage 2: inbox → handler (fresh context so EF does not serve from identity map).
        await using var processContext = CreateDbContext();
        var registry = new MessageTypeRegistry();
        registry.Register<TestDurableCommand>("ModularTemplate.operations.test-command.v1");
        var invocationTracker = new HandlerInvocationTracker();
        ServiceProvider sp = new ServiceCollection()
            .AddSingleton(invocationTracker)
            .AddScoped<IDurableCommandHandler<TestDurableCommand>, TestDurableCommandHandler>()
            .BuildServiceProvider();
        try
        {
            var processor = new InboxProcessor(
                sp,
                [(IModuleDbContext)processContext],
                registry,
                Options.Create(new DurableMessagingOptions { BatchSize = 20 }),
                NullLogger<InboxProcessor>.Instance);
            await processor.ProcessPendingAsync(CancellationToken.None);
            invocationTracker.HandledJobIds.ShouldBe(["pipeline-job"]);
            (await processContext.InboxMessages.SingleAsync(CancellationToken.None))
                .Status.ShouldBe(PersistedMessageStatus.Processed);
        }
        finally
        {
            await sp.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DispatchPendingAsync_WhenMessageIsStaleProcessing_ReclaimsAndDispatchesIt()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);
        OutboxMessage outboxMessage = OutboxMessage.Create(
            messageId: Guid.NewGuid(),
            messageKind: MessageKind.Command,
            messageType: "ModularTemplate.ops.do-work.v1",
            sourceModule: "ops",
            targetModule: "ops",
            correlationId: Guid.NewGuid(),
            causationId: null,
            operationId: null,
            payload: "{}");
        dbContext.OutboxMessages.Add(outboxMessage);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        // Simulate a stale Processing lock left by a worker that crashed.
        DateTimeOffset staleLockTime = DateTimeOffset.UtcNow - TimeSpan.FromHours(1);
        await dbContext.Database.ExecuteSqlAsync(
            $"""
            UPDATE identity.outbox_messages
            SET "Status" = 'Processing', "LockedAtUtc" = {staleLockTime}, "LockedBy" = 'dead-worker-xyz'
            WHERE "Id" = {outboxMessage.Id}
            """,
            CancellationToken.None);
        dbContext.ChangeTracker.Clear();

        // LockTimeout = 2 min; the lock is 1 hour old → stale → must be reclaimed.
        var dispatcher = new OutboxDispatcher(
            [(IModuleDbContext)dbContext],
            new TestOutboxTransport(dbContext),
            new LocalSubscriptionRegistry(),
            Options.Create(new DurableMessagingOptions { BatchSize = 20, LockTimeout = TimeSpan.FromMinutes(2) }),
            NullLogger<OutboxDispatcher>.Instance);

        int count = await dispatcher.DispatchPendingAsync(CancellationToken.None);

        count.ShouldBe(1);
        dbContext.ChangeTracker.Clear();
        (await dbContext.OutboxMessages.SingleAsync(CancellationToken.None))
            .Status.ShouldBe(PersistedMessageStatus.Processed);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DispatchPendingAsync_WhenTwoWorkersRunConcurrently_EachMessageClaimedOnlyOnce()
    {
        await using var setupContext = CreateDbContext();
        await setupContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await setupContext.Database.EnsureCreatedAsync(CancellationToken.None);
        const int messageCount = 5;
        for (int i = 0; i < messageCount; i++)
        {
            setupContext.OutboxMessages.Add(OutboxMessage.Create(
                messageId: Guid.NewGuid(),
                messageKind: MessageKind.Command,
                messageType: $"ModularTemplate.ops.work.v{i + 1}",
                sourceModule: "ops",
                targetModule: "ops",
                correlationId: Guid.NewGuid(),
                causationId: null,
                operationId: null,
                payload: "{}"));
        }
        await setupContext.SaveChangesAsync(CancellationToken.None);

        await using var context1 = CreateDbContext();
        await using var context2 = CreateDbContext();
        var transport1 = new CountingOutboxTransport();
        var transport2 = new CountingOutboxTransport();
        var opts = Options.Create(new DurableMessagingOptions { BatchSize = messageCount });

        int[] counts = await Task.WhenAll(
            new OutboxDispatcher([(IModuleDbContext)context1], transport1, new LocalSubscriptionRegistry(), opts,
                NullLogger<OutboxDispatcher>.Instance).DispatchPendingAsync(CancellationToken.None),
            new OutboxDispatcher([(IModuleDbContext)context2], transport2, new LocalSubscriptionRegistry(), opts,
                NullLogger<OutboxDispatcher>.Instance).DispatchPendingAsync(CancellationToken.None));

        (counts[0] + counts[1]).ShouldBe(messageCount);

        await using var verifyContext = CreateDbContext();
        int processedCount = await verifyContext.OutboxMessages
            .CountAsync(x => x.Status == PersistedMessageStatus.Processed, CancellationToken.None);
        processedCount.ShouldBe(messageCount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DispatchPendingAsync_WhenOperationsOutboxMessageExists_UsesOperationsSchema()
    {
        await using OperationsDbContext dbContext = CreateOperationsDbContext();
        await dbContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);
        dbContext.OutboxMessages.Add(OutboxMessage.Create(
            messageId: Guid.NewGuid(),
            messageKind: MessageKind.Command,
            messageType: "ModularTemplate.operations.rebuild-read-model.v1",
            sourceModule: "operations",
            targetModule: "operations",
            correlationId: Guid.NewGuid(),
            causationId: null,
            operationId: null,
            payload: "{}"));
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var dispatcher = new OutboxDispatcher(
            [(IModuleDbContext)dbContext],
            new CountingOutboxTransport(),
            new LocalSubscriptionRegistry(),
            Options.Create(new DurableMessagingOptions { BatchSize = 20 }),
            NullLogger<OutboxDispatcher>.Instance);

        int dispatchedCount = await dispatcher.DispatchPendingAsync(CancellationToken.None);

        dispatchedCount.ShouldBe(1);
        dbContext.ChangeTracker.Clear();
        OutboxMessage outbox = await dbContext.OutboxMessages.SingleAsync(CancellationToken.None);
        outbox.Status.ShouldBe(PersistedMessageStatus.Processed);
    }

    private IdentityDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(postgreSqlFixture.ConnectionString)
            .Options;

        return new IdentityDbContext(options);
    }

    private OperationsDbContext CreateOperationsDbContext()
    {
        var options = new DbContextOptionsBuilder<OperationsDbContext>()
            .UseNpgsql(postgreSqlFixture.ConnectionString)
            .Options;

        return new OperationsDbContext(options);
    }

    private sealed record TestDurableCommand(string JobId) : IDurableCommand;

    private sealed class TestDurableCommandHandler(HandlerInvocationTracker tracker)
        : IDurableCommandHandler<TestDurableCommand>
    {
        public Task HandleAsync(
            TestDurableCommand command,
            MessageContext context,
            CancellationToken cancellationToken)
        {
            tracker.HandledJobIds.Add(command.JobId);
            return Task.CompletedTask;
        }
    }

    private sealed class HandlerInvocationTracker
    {
        public List<string> HandledJobIds { get; } = [];
    }

    private sealed record FailingCommand(string JobId) : IDurableCommand;

    private sealed class AlwaysFailingCommandHandler : IDurableCommandHandler<FailingCommand>
    {
        public Task HandleAsync(FailingCommand command, MessageContext context, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Simulated processing failure.");
        }
    }

    private sealed class TestOutboxTransport(IdentityDbContext dbContext) : IOutboxTransport
    {
        public Task DispatchAsync(OutboxMessage outboxMessage, string targetModule, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(targetModule);

            dbContext.InboxMessages.Add(InboxMessage.Create(
                outboxMessage.MessageId,
                outboxMessage.MessageKind,
                outboxMessage.MessageType,
                outboxMessage.SourceModule,
                targetModule,
                outboxMessage.CorrelationId,
                outboxMessage.CausationId,
                outboxMessage.OperationId,
                idempotencyKey: null,
                outboxMessage.Payload,
                outboxMessage.Metadata,
                outboxMessage.MaxAttempts));

            return Task.CompletedTask;
        }
    }

    /// <summary>Counts dispatch calls without side effects; used to verify no double-claiming.</summary>
    private sealed class CountingOutboxTransport : IOutboxTransport
    {
        private int _count;

        public int Count => _count;

        public Task DispatchAsync(OutboxMessage outboxMessage, string targetModule, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _count);
            return Task.CompletedTask;
        }
    }

    /// <summary>Always throws; used to exercise the outbox dead-letter path.</summary>
    private sealed class ThrowingOutboxTransport : IOutboxTransport
    {
        public Task DispatchAsync(OutboxMessage outboxMessage, string targetModule, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated transport failure.");
    }
}
