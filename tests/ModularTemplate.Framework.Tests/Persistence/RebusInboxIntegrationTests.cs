using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModularTemplate.Identity.Infrastructure.Persistence;
using ModularTemplate.Identity.Infrastructure.Tests.Support;
using ModularTemplate.Infrastructure.Outbox;
using ModularTemplate.Infrastructure.Persistence;
using ModularTemplate.Infrastructure.Transport;
using ModularTemplate.SharedKernel.Messaging;
using Shouldly;

namespace ModularTemplate.Identity.Infrastructure.Tests.Persistence;

public sealed class RebusInboxIntegrationTests(PostgreSqlFixture postgreSqlFixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task DispatchPendingAsync_WhenCommandIsDeliveredTwice_RunsModuleHandlerOnce()
    {
        await using IdentityDbContext setupContext = CreateDbContext();
        await setupContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await setupContext.Database.EnsureCreatedAsync(CancellationToken.None);
        await setupContext.Database.ExecuteSqlRawAsync(
            "CREATE SCHEMA IF NOT EXISTS transport",
            CancellationToken.None);
        Guid messageId = Guid.NewGuid();
        OutboxMessage outboxMessage = OutboxMessage.Create(
            messageId,
            MessageKind.Command,
            "test.rebus-inbox-command.v1",
            sourceModule: "identity",
            targetModule: "identity",
            correlationId: Guid.NewGuid(),
            causationId: null,
            operationId: null,
            payload: "{\"Value\":\"hello\"}");
        setupContext.OutboxMessages.Add(outboxMessage);
        await setupContext.SaveChangesAsync(CancellationToken.None);

        using IHost host = CreateHost();
        await host.StartAsync(CancellationToken.None);
        try
        {
            HandledMessageCounter counter = host.Services.GetRequiredService<HandledMessageCounter>();
            await WaitUntilAsync(() => counter.Count == 1, TimeSpan.FromSeconds(5));

            await using (AsyncServiceScope duplicateScope = host.Services.CreateAsyncScope())
            {
                IOutboxTransport transport = duplicateScope.ServiceProvider.GetRequiredService<IOutboxTransport>();
                await transport.DispatchAsync(outboxMessage, CancellationToken.None);
            }
            await Task.Delay(TimeSpan.FromMilliseconds(500), CancellationToken.None);

            counter.Count.ShouldBe(1);
            await using AsyncServiceScope verifyScope = host.Services.CreateAsyncScope();
            IdentityDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            (await verifyContext.InboxMessages.CountAsync(CancellationToken.None)).ShouldBe(1);
            verifyContext.InboxMessages.Single().IsProcessed.ShouldBeTrue();
            verifyContext.OutboxMessages.Single().Status.ShouldBe(PersistedMessageStatus.Processed);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    private IHost CreateHost()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:modular-template-host"] = postgreSqlFixture.ConnectionString;
        builder.Configuration["Messaging:QueuePrefix"] = $"test-{Guid.NewGuid():N}";
        builder.Services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(postgreSqlFixture.ConnectionString));
        builder.AddTransport();
        builder.Services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<IdentityDbContext>());
        builder.Services.AddModulePersistence<IdentityDbContext>("identity");
        builder.Services.AddModuleMessaging("identity", typeof(TestRebusInboxCommandHandler));
        builder.Services.AddSingleton<HandledMessageCounter>();

        return builder.Build();
    }

    private IdentityDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(postgreSqlFixture.ConnectionString)
            .Options;

        return new IdentityDbContext(options);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < expiresAt)
        {
            if (predicate())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), CancellationToken.None);
        }

        predicate().ShouldBeTrue();
    }

    [MessageIdentity("test.rebus-inbox-command.v1")]
    private sealed record TestRebusInboxCommand(string Value) : IDurableCommand;

    private sealed class TestRebusInboxCommandHandler(HandledMessageCounter counter)
        : IModuleMessageHandler<TestRebusInboxCommand>
    {
        public Task HandleAsync(TestRebusInboxCommand message, CancellationToken cancellationToken)
        {
            counter.Increment();
            return Task.CompletedTask;
        }
    }

    private sealed class HandledMessageCounter
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public void Increment()
        {
            Interlocked.Increment(ref _count);
        }
    }
}
