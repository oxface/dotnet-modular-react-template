using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModularTemplate.Identity.Infrastructure.Persistence;
using ModularTemplate.Identity.Infrastructure.Tests.Support;
using ModularTemplate.Infrastructure.Outbox;
using ModularTemplate.Infrastructure.Persistence;
using ModularTemplate.Operations.Infrastructure.Persistence;
using ModularTemplate.Infrastructure.Transport;
using ModularTemplate.SharedKernel.Messaging;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Routing.TypeBased;
using Rebus.Transport.InMem;
using Shouldly;

namespace ModularTemplate.Identity.Infrastructure.Tests.Persistence;

/// <summary>
/// Tests the Rebus transport layer: the handler that receives
/// <see cref="DurableTransportEnvelope"/> messages and writes inbox rows, and the
/// end-to-end roundtrip from <see cref="RebusOutboxTransport"/> through an in-memory
/// bus to the handler.
/// </summary>
public sealed class RebusTransportTests(PostgreSqlFixture postgreSqlFixture)
    : IClassFixture<PostgreSqlFixture>
{
    // -------------------------------------------------------------------------
    // Direct handler tests (no bus machinery required)
    // -------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RebusDurableTransportHandler_WhenEnvelopeReceived_WritesInboxRow()
    {
        await using IdentityDbContext dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);

        Guid messageId = Guid.NewGuid();
        var envelope = new DurableTransportEnvelope(
            messageId,
            MessageKind.Command,
            "ModularTemplate.notifications.send-email.v1",
            SourceModule: "notifications",
            TargetModule: "identity",
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OperationId: null,
            Payload: "{\"subject\":\"welcome\"}",
            MetadataJson: null,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            MaxAttempts: 5);

        var handler = new RebusDurableTransportHandler(
            [(IModuleDbContext)dbContext],
            Microsoft.Extensions.Options.Options.Create(new DurableMessagingOptions()));

        await handler.Handle(envelope);

        IModuleDbContext iCtx = dbContext;
        InboxMessage inbox = await iCtx.InboxMessages.SingleAsync(CancellationToken.None);
        inbox.MessageId.ShouldBe(messageId);
        inbox.MessageType.ShouldBe("ModularTemplate.notifications.send-email.v1");
        inbox.SourceModule.ShouldBe("notifications");
        inbox.TargetModule.ShouldBe("identity");
        inbox.Status.ShouldBe(PersistedMessageStatus.Pending);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RebusDurableTransportHandler_WhenDuplicateEnvelopeReceived_WritesOnlyOneInboxRow()
    {
        await using IdentityDbContext dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);

        var envelope = new DurableTransportEnvelope(
            MessageId: Guid.NewGuid(),
            MessageKind: MessageKind.Event,
            MessageType: "ModularTemplate.identity.user-created.v1",
            SourceModule: "identity",
            TargetModule: "identity",
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OperationId: null,
            Payload: "{}",
            MetadataJson: null,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            MaxAttempts: 5);

        var handler2 = new RebusDurableTransportHandler(
            [(IModuleDbContext)dbContext],
            Microsoft.Extensions.Options.Options.Create(new DurableMessagingOptions()));

        // First delivery.
        await handler2.Handle(envelope);
        // Duplicate delivery (at-least-once transport guarantee).
        await handler2.Handle(envelope);

        // Handler must deduplicate on (MessageId, TargetModule).
        IModuleDbContext iCtx2 = dbContext;
        int count = await iCtx2.InboxMessages.CountAsync(CancellationToken.None);
        count.ShouldBe(1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RebusDurableTransportHandler_WhenTargetModuleIsOperations_WritesOnlyOperationsInbox()
    {
        await using IdentityDbContext identityContext = CreateDbContext();
        await identityContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await identityContext.Database.EnsureCreatedAsync(CancellationToken.None);
        await using OperationsDbContext operationsContext = CreateOperationsDbContext();
        await operationsContext.Database.ExecuteSqlRawAsync(
            operationsContext.Database.GenerateCreateScript(),
            CancellationToken.None);

        Guid messageId = Guid.NewGuid();
        var envelope = new DurableTransportEnvelope(
            messageId,
            MessageKind.Event,
            "ModularTemplate.identity.local-user-created.v1",
            SourceModule: "identity",
            TargetModule: "operations",
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OperationId: null,
            Payload: "{}",
            MetadataJson: null,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            MaxAttempts: 5);

        var handler = new RebusDurableTransportHandler(
            [(IModuleDbContext)identityContext, (IModuleDbContext)operationsContext],
            Microsoft.Extensions.Options.Options.Create(new DurableMessagingOptions()));

        await handler.Handle(envelope);

        int identityInboxCount = await identityContext.InboxMessages.CountAsync(CancellationToken.None);
        InboxMessage operationsInbox =
            await operationsContext.InboxMessages.SingleAsync(CancellationToken.None);
        identityInboxCount.ShouldBe(0);
        operationsInbox.MessageId.ShouldBe(messageId);
        operationsInbox.SourceModule.ShouldBe("identity");
        operationsInbox.TargetModule.ShouldBe("operations");
        operationsInbox.Status.ShouldBe(PersistedMessageStatus.Pending);
    }

    // -------------------------------------------------------------------------
    // End-to-end Rebus transport test
    // -------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RebusOutboxTransport_WhenEnvelopeSent_HandlerEventuallyWritesInboxRow()
    {
        // Reset the DB using a short-lived context.
        await using (IdentityDbContext setupContext = CreateDbContext())
        {
            await setupContext.Database.EnsureDeletedAsync(CancellationToken.None);
            await setupContext.Database.EnsureCreatedAsync(CancellationToken.None);
        }

        // Build a service provider with Rebus wired to an in-memory transport so that
        // RebusDurableTransportHandler runs in a background worker exactly as in production.
        var network = new InMemNetwork();
        string connectionString = postgreSqlFixture.ConnectionString;

        await using ServiceProvider serviceProvider = new ServiceCollection()
            .AddDbContext<IdentityDbContext>(opt => opt.UseNpgsql(connectionString))
            .AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<IdentityDbContext>())
            .Configure<DurableMessagingOptions>(_ => { })
            .AddRebus(configure => configure
                .Transport(t => t.UseInMemoryTransport(network, "test-queue"))
                .Routing(r => r.TypeBased().Map<DurableTransportEnvelope>("test-queue")))
            .AutoRegisterHandlersFromAssemblyOf<RebusDurableTransportHandler>()
            .BuildServiceProvider();

        // Start the Rebus background worker.
        serviceProvider.StartHostedServices();

        IBus bus = serviceProvider.GetRequiredService<IBus>();
        var transport = new RebusOutboxTransport(bus);

        Guid messageId = Guid.NewGuid();
        OutboxMessage outboxMessage = OutboxMessage.Create(
            messageId,
            MessageKind.Command,
            "ModularTemplate.notifications.send-email.v1",
            sourceModule: "notifications",
            targetModule: "identity",
            correlationId: Guid.NewGuid(),
            causationId: null,
            operationId: null,
            payload: "{\"subject\":\"test\"}");

        // Dispatch via the real transport — puts a DurableTransportEnvelope on the bus.
        await transport.DispatchAsync(outboxMessage, "identity", CancellationToken.None);

        // Poll with a fresh context until the handler commits the inbox row.
        // The Rebus worker picks up the message asynchronously.
        await using IdentityDbContext pollContext = CreateDbContext();
        InboxMessage inbox = await WaitForAsync(
            () => ((IModuleDbContext)pollContext).InboxMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.MessageId == messageId, CancellationToken.None),
            timeout: TimeSpan.FromSeconds(10));

        inbox.TargetModule.ShouldBe("identity");
        inbox.MessageType.ShouldBe("ModularTemplate.notifications.send-email.v1");
        inbox.Status.ShouldBe(PersistedMessageStatus.Pending);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private IdentityDbContext CreateDbContext()
    {
        DbContextOptions<IdentityDbContext> options =
            new DbContextOptionsBuilder<IdentityDbContext>()
                .UseNpgsql(postgreSqlFixture.ConnectionString)
                .Options;

        return new IdentityDbContext(options);
    }

    private OperationsDbContext CreateOperationsDbContext()
    {
        DbContextOptions<OperationsDbContext> options =
            new DbContextOptionsBuilder<OperationsDbContext>()
                .UseNpgsql(postgreSqlFixture.ConnectionString)
                .Options;

        return new OperationsDbContext(options);
    }

    /// <summary>
    /// Polls <paramref name="condition"/> every 50 ms until it returns a non-null value or
    /// <paramref name="timeout"/> elapses.
    /// </summary>
    private static async Task<T> WaitForAsync<T>(Func<Task<T?>> condition, TimeSpan timeout)
        where T : class
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            T? result = await condition();
            if (result is not null)
            {
                return result;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException(
            $"Expected condition was not satisfied within {timeout.TotalSeconds}s.");
    }
}
