using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Bondstone.EntityFrameworkCore.Inbox;
using ModularTemplate.Identity.Infrastructure.Persistence;
using ModularTemplate.Identity.Infrastructure.Tests.Support;
using Bondstone.EntityFrameworkCore.Outbox;
using Bondstone.Messaging;
using Bondstone.EntityFrameworkCore.Persistence;
using Bondstone.EntityFrameworkCore.Persistence.DomainEvents;
using Bondstone.EntityFrameworkCore.Postgres.Persistence;
using Bondstone.Transport.Rebus;
using Shouldly;

namespace ModularTemplate.Identity.Infrastructure.Tests.Persistence;

public sealed class RebusInboxIntegrationTests(PostgreSqlFixture postgreSqlFixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task DispatchPendingAsync_WhenCommandIsDeliveredTwice_RunsModuleHandlerAndOutgoingCommandOnce()
    {
        await using IdentityDbContext setupContext = CreateDbContext();
        await setupContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await setupContext.Database.EnsureCreatedAsync(CancellationToken.None);
        Guid messageId = Guid.NewGuid();
        OutboxMessage outboxMessage = OutboxMessage.Create(
            messageId,
            MessageKind.Command,
            "test.rebus-inbox-command.v1",
            sourceModule: "identity",
            targetModule: "identity",
            correlationId: Guid.NewGuid(),
            causationId: null,
            durableOperationId: null,
            payload: "{\"Value\":\"hello\"}");
        setupContext.OutboxMessages.Add(outboxMessage);
        await setupContext.SaveChangesAsync(CancellationToken.None);

        using IHost host = CreateHost();
        await host.StartAsync(CancellationToken.None);
        try
        {
            HandledMessageCounter counter = host.Services.GetRequiredService<HandledMessageCounter>();
            await WaitUntilAsync(() => counter.Count == 1, TimeSpan.FromSeconds(5));
            await WaitUntilAsync(
                async () => await CountFollowUpCommandsAsync(host) == 1,
                TimeSpan.FromSeconds(5));

            await using (AsyncServiceScope duplicateScope = host.Services.CreateAsyncScope())
            {
                IOutboxTransport transport = duplicateScope.ServiceProvider.GetRequiredService<IOutboxTransport>();
                await transport.DispatchAsync(outboxMessage, CancellationToken.None);
            }
            await Task.Delay(TimeSpan.FromMilliseconds(500), CancellationToken.None);

            counter.Count.ShouldBe(1);
            await using AsyncServiceScope verifyScope = host.Services.CreateAsyncScope();
            IdentityDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            (await verifyContext.InboxMessages.CountAsync(
                x => x.MessageId == messageId.ToString("D"),
                CancellationToken.None)).ShouldBe(1);
            (await verifyContext.InboxMessages.SingleAsync(
                    x => x.MessageId == messageId.ToString("D"),
                    CancellationToken.None))
                .IsProcessed
                .ShouldBeTrue();
            (await verifyContext.OutboxMessages.SingleAsync(
                    x => x.MessageId == messageId,
                    CancellationToken.None))
                .Status
                .ShouldBe(PersistedMessageStatus.Processed);
            (await verifyContext.OutboxMessages.CountAsync(
                    x => x.MessageType == "test.rebus-follow-up-command.v1",
                    CancellationToken.None))
                .ShouldBe(1);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DispatchPendingAsync_WhenCommandTargetsDifferentModule_CommitsTargetModuleInboxAndState()
    {
        await using IdentityDbContext identitySetupContext = CreateIdentityDbContext();
        await identitySetupContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await identitySetupContext.Database.EnsureCreatedAsync(CancellationToken.None);
        await using ProductsTestDbContext productsSetupContext = CreateProductsDbContext();
        await productsSetupContext.Database.ExecuteSqlRawAsync(
            productsSetupContext.Database.GenerateCreateScript(),
            CancellationToken.None);
        Guid messageId = Guid.NewGuid();
        OutboxMessage outboxMessage = OutboxMessage.Create(
            messageId,
            MessageKind.Command,
            "test.cross-module-product-command.v1",
            sourceModule: "identity",
            targetModule: "products",
            correlationId: Guid.NewGuid(),
            causationId: null,
            durableOperationId: null,
            payload: "{\"ProductName\":\"template.cross-module-smoke\"}");
        identitySetupContext.OutboxMessages.Add(outboxMessage);
        await identitySetupContext.SaveChangesAsync(CancellationToken.None);

        using IHost host = CreateIdentityAndProductsHost();
        await host.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(
                async () => await CountProductsAsync(host) == 1,
                TimeSpan.FromSeconds(5));

            await using AsyncServiceScope verifyScope = host.Services.CreateAsyncScope();
            IdentityDbContext identityContext = verifyScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            ProductsTestDbContext productsContext = verifyScope.ServiceProvider.GetRequiredService<ProductsTestDbContext>();

            (await identityContext.OutboxMessages.SingleAsync(
                    x => x.MessageId == messageId,
                    CancellationToken.None))
                .Status
                .ShouldBe(PersistedMessageStatus.Processed);
            (await productsContext.InboxMessages.SingleAsync(
                    x => x.MessageId == messageId.ToString("D"),
                    CancellationToken.None))
                .IsProcessed
                .ShouldBeTrue();
            TestProductRecord product = await productsContext.Products.SingleAsync(CancellationToken.None);
            product.Name.ShouldBe("template.cross-module-smoke");
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
        builder.Configuration["Messaging:Rebus:QueuePrefix"] = $"test-{Guid.NewGuid():N}";
        builder.Services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(postgreSqlFixture.ConnectionString));
        builder.AddRebusTransport(transport =>
            transport.UsePostgresInternalTransport(builder.Configuration.GetSection("Messaging:Rebus")));
        builder.Services.AddModulePersistence<IdentityDbContext>("identity");
        builder.Services.AddModuleMessaging("identity", typeof(TestRebusInboxCommandHandler));
        builder.Services.AddSingleton<HandledMessageCounter>();

        return builder.Build();
    }

    private IdentityDbContext CreateDbContext()
    {
        return CreateIdentityDbContext();
    }

    private IHost CreateIdentityAndProductsHost()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:modular-template-host"] = postgreSqlFixture.ConnectionString;
        builder.Configuration["Messaging:Rebus:QueuePrefix"] = $"test-{Guid.NewGuid():N}";
        builder.Services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(postgreSqlFixture.ConnectionString));
        builder.Services.AddDbContext<ProductsTestDbContext>(options =>
            options.UseNpgsql(postgreSqlFixture.ConnectionString));
        builder.AddRebusTransport(transport =>
            transport.UsePostgresInternalTransport(builder.Configuration.GetSection("Messaging:Rebus")));
        builder.Services.AddModulePersistence<IdentityDbContext>("identity");
        builder.Services.AddModulePersistence<ProductsTestDbContext>("products");
        builder.Services.AddModuleMessaging("products", typeof(TestCrossModuleProductCommandHandler));

        return builder.Build();
    }

    private IdentityDbContext CreateIdentityDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(postgreSqlFixture.ConnectionString)
            .Options;

        return new IdentityDbContext(options);
    }

    private ProductsTestDbContext CreateProductsDbContext()
    {
        var options = new DbContextOptionsBuilder<ProductsTestDbContext>()
            .UseNpgsql(postgreSqlFixture.ConnectionString)
            .Options;

        return new ProductsTestDbContext(options);
    }

    private static Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        return WaitUntilAsync(() => Task.FromResult(predicate()), timeout);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> predicate, TimeSpan timeout)
    {
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < expiresAt)
        {
            if (await predicate())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), CancellationToken.None);
        }

        (await predicate()).ShouldBeTrue();
    }

    private static async Task<int> CountFollowUpCommandsAsync(IHost host)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IdentityDbContext dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        return await dbContext.OutboxMessages.CountAsync(
            x => x.MessageType == "test.rebus-follow-up-command.v1",
            CancellationToken.None);
    }

    private static async Task<int> CountProductsAsync(IHost host)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        ProductsTestDbContext dbContext = scope.ServiceProvider.GetRequiredService<ProductsTestDbContext>();
        return await dbContext.Products.CountAsync(CancellationToken.None);
    }

    [DurableCommandIdentity("test.rebus-inbox-command.v1")]
    private sealed record TestRebusInboxCommand(string Value) : IDurableCommand;

    [DurableCommandIdentity("test.rebus-follow-up-command.v1")]
    private sealed record TestFollowUpCommand(string Value) : IDurableCommand;

    [DurableCommandIdentity("test.cross-module-product-command.v1")]
    private sealed record TestCrossModuleProductCommand(string ProductName) : IDurableCommand;

    private sealed class TestRebusInboxCommandHandler(
        IDurableCommandSender durableCommandSender,
        HandledMessageCounter counter)
        : IModuleMessageHandler<TestRebusInboxCommand>
    {
        public Task HandleAsync(TestRebusInboxCommand message, CancellationToken cancellationToken)
        {
            durableCommandSender.Send(
                new TestFollowUpCommand(message.Value),
                targetModule: "identity");
            counter.Increment();
            return Task.CompletedTask;
        }
    }

    private sealed class TestFollowUpCommandHandler : IModuleMessageHandler<TestFollowUpCommand>
    {
        public Task HandleAsync(TestFollowUpCommand message, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestCrossModuleProductCommandHandler(ProductsTestDbContext dbContext)
        : IModuleMessageHandler<TestCrossModuleProductCommand>
    {
        public Task HandleAsync(TestCrossModuleProductCommand message, CancellationToken cancellationToken)
        {
            dbContext.Products.Add(new TestProductRecord(Guid.NewGuid(), message.ProductName));
            return Task.CompletedTask;
        }
    }

    private sealed class ProductsTestDbContext(DbContextOptions<ProductsTestDbContext> options)
        : DbContext(options), IModuleDbContext
    {
        public DbSet<TestProductRecord> Products => Set<TestProductRecord>();

        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

        public DbSet<StoredDomainEvent> DomainEvents => Set<StoredDomainEvent>();

        string IModuleDbContext.ModuleName => "products";

        DbSet<OutboxMessage> IModuleDbContext.OutboxMessages => OutboxMessages;

        DbSet<InboxMessage> IModuleDbContext.InboxMessages => InboxMessages;

        DbSet<StoredDomainEvent> IModuleDbContext.DomainEvents => DomainEvents;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("products");
            modelBuilder.Entity<TestProductRecord>(builder =>
            {
                builder.ToTable("test_products", "products");
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
            });
            modelBuilder.ApplyPostgresModuleMessagingPersistence("products");
        }
    }

    private sealed class TestProductRecord
    {
        private TestProductRecord()
        {
        }

        public TestProductRecord(Guid id, string name)
        {
            Id = id;
            Name = name;
        }

        public Guid Id { get; private set; }

        public string Name { get; private set; } = string.Empty;
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
