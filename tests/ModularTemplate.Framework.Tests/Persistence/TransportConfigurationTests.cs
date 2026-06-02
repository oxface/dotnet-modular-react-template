using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ModularTemplate.Identity.Infrastructure.Persistence;
using Bondstone.EntityFrameworkCore.Inbox;
using Bondstone.EntityFrameworkCore.Outbox;
using Bondstone.Messaging;
using Bondstone.EntityFrameworkCore.Persistence;
using Bondstone.EntityFrameworkCore.Persistence.DomainEvents;
using Bondstone.EntityFrameworkCore.Postgres.Outbox;
using Bondstone.EntityFrameworkCore.Postgres.Persistence;
using Bondstone.Transport.Rebus;
using Rebus.ServiceProvider;
using Shouldly;

namespace ModularTemplate.Identity.Infrastructure.Tests.Persistence;

public sealed class TransportConfigurationTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void AddRebusTransport_RegistersRebusRuntimeServices()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Messaging:Modules:0"] = "identity";

        builder.AddRebusTransport(transport =>
            transport.UsePostgresInternalTransport(builder.Configuration.GetSection("Messaging:Rebus")));

        builder.Services.Any(service => service.ServiceType == typeof(IOutboxTransport)).ShouldBeTrue();
        builder.Services.Any(service => service.ServiceType == typeof(IOutboxRouteResolver)).ShouldBeTrue();
        builder.Services.Any(service => service.ServiceType == typeof(IBusRegistry)).ShouldBeTrue();
        builder.Services.Any(service => service.ServiceType == typeof(RebusPostgresSchemaInitializer)).ShouldBeTrue();
        builder.Services.Any(service =>
            service.ServiceType == typeof(IHostedService)
            && service.ImplementationType?.Name?.Contains("SchemaInitializer", StringComparison.Ordinal) == true)
            .ShouldBeFalse();
        using IHost host = builder.Build();
        host.Services.GetServices<IHostedService>()
            .Count(service => service.GetType().Name == "RebusSubscriptionHostedService")
            .ShouldBe(2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddRebusTransport_WhenMultipleModulesAreConfigured_AddsOneSubscriptionServicePerModule()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Messaging:Modules:0"] = "identity";
        builder.Configuration["Messaging:Modules:1"] = "operations";

        builder.AddRebusTransport(transport =>
            transport.UsePostgresInternalTransport(builder.Configuration.GetSection("Messaging:Rebus")));

        using IHost host = builder.Build();
        host.Services.GetServices<IHostedService>()
            .Count(service => service.GetType().Name == "RebusSubscriptionHostedService")
            .ShouldBe(2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddModulePersistence_RegistersPostgresRuntimeServices()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.Services.AddModulePersistence<IdentityDbContext>("identity");

        builder.Services.Any(service =>
            service.ServiceType == typeof(IOutboxDispatchLock)
            && service.ImplementationType == typeof(PostgresAdvisoryOutboxDispatchLock))
            .ShouldBeTrue();
        builder.Services.Any(service => service.ServiceType == typeof(IOutboxDispatcher)).ShouldBeTrue();
        builder.Services.Any(service => service.ServiceType == typeof(OutboxDispatcher<IdentityDbContext>)).ShouldBeTrue();
        builder.Services.Any(service => service.ServiceType == typeof(IModuleMessageInbox)).ShouldBeTrue();
        builder.Services.Any(service =>
            service.ServiceType == typeof(IModuleMessageInboxExecutor)
            && service.ImplementationType == typeof(EntityFrameworkCoreModuleMessageInbox<IdentityDbContext>))
            .ShouldBeTrue();
        builder.Services.Any(service =>
            service.ServiceType == typeof(IModuleBoundaryExecutor)
            && service.ImplementationType == typeof(EntityFrameworkCoreModuleBoundary<IdentityDbContext>))
            .ShouldBeTrue();
        builder.Services.Any(service =>
            service.ServiceType == typeof(IHostedService)
            && service.ImplementationType == typeof(OutboxDispatcherBackgroundService<IdentityDbContext>))
            .ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddModulePersistence_WhenMultipleModulesAreRegistered_AddsOneOutboxWorkerPerModule()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.Services.AddModulePersistence<IdentityDbContext>("identity");
        builder.Services.AddModulePersistence<OperationsTransportDbContext>("operations");

        builder.Services
            .Where(service => service.ServiceType == typeof(IHostedService)
                && service.ImplementationType?.IsGenericType == true
                && service.ImplementationType.GetGenericTypeDefinition() == typeof(OutboxDispatcherBackgroundService<>))
            .Select(service => service.ImplementationType)
            .OrderBy(type => type!.FullName, StringComparer.Ordinal)
            .ShouldBe([
                typeof(OutboxDispatcherBackgroundService<IdentityDbContext>),
                typeof(OutboxDispatcherBackgroundService<OperationsTransportDbContext>)
            ]);
        builder.Services.Any(service => service.ServiceType == typeof(OutboxDispatcher<IdentityDbContext>)).ShouldBeTrue();
        builder.Services.Any(service => service.ServiceType == typeof(OutboxDispatcher<OperationsTransportDbContext>)).ShouldBeTrue();
        builder.Services.Count(service => service.ServiceType == typeof(IModuleMessageInboxExecutor))
            .ShouldBe(2);
        builder.Services.Count(service => service.ServiceType == typeof(IModuleBoundaryExecutor))
            .ShouldBe(2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddRebusTransport_WhenInternalTransportIsMissing_Throws()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => builder.AddRebusTransport());

        exception.Message.ShouldContain("Rebus internal transport is not configured");
        exception.Message.ShouldContain(nameof(RebusPostgresTransportBuilderExtensions.UsePostgresInternalTransport));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddRebusTransport_WhenConfiguredInCode_OverridesConfiguration()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Messaging:Modules:0"] = "identity";
        builder.Configuration["Messaging:Rebus:QueuePrefix"] = "configured";

        builder.AddRebusTransport(transport => transport
            .UsePostgresInternalTransport(builder.Configuration.GetSection("Messaging:Rebus"))
            .Configure(options => options.QueuePrefix = "from-code"));
        using IHost host = builder.Build();

        host.Services.GetRequiredService<IOptions<RebusTransportOptions>>()
            .Value
            .QueuePrefix
            .ShouldBe("from-code");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddRebusTransport_WhenTopologyOptionsAreInvalid_FailsOptionsValidation()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Messaging:Modules:0"] = "";
        builder.Configuration["Messaging:Rebus:QueuePrefix"] = "";
        builder.Configuration["Messaging:PollingInterval"] = "00:00:00";
        builder.Configuration["Messaging:RetryDelays:0"] = "-00:00:01";

        builder.AddRebusTransport(transport =>
            transport.UsePostgresInternalTransport(builder.Configuration.GetSection("Messaging:Rebus")));
        using IHost host = builder.Build();

        OptionsValidationException exception = Should.Throw<OptionsValidationException>(
            () => host.Services.GetRequiredService<IOptions<DurableMessagingOptions>>().Value);
        exception.Message.ShouldContain("Messaging:PollingInterval");
        exception.Message.ShouldContain("Messaging:RetryDelays");

        OptionsValidationException rebusException = Should.Throw<OptionsValidationException>(
            () => host.Services.GetRequiredService<IOptions<RebusTransportOptions>>().Value);
        rebusException.Message.ShouldContain("Messaging:Rebus:QueuePrefix");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddRebusTransport_WhenMessagingSqlIdentifiersAreInvalid_FailsOptionsValidation()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Messaging:Modules:0"] = "identity-module";
        builder.Configuration["Messaging:Rebus:Postgres:TransportSchema"] = "transport-schema";
        builder.Configuration["Messaging:Rebus:Postgres:TransportTable"] = "rebus messages";
        builder.Configuration["Messaging:Rebus:Postgres:SubscriptionTable"] = "rebus-subscriptions";

        builder.AddRebusTransport(transport =>
            transport.UsePostgresInternalTransport(builder.Configuration.GetSection("Messaging:Rebus")));
        using IHost host = builder.Build();

        OptionsValidationException exception = Should.Throw<OptionsValidationException>(
            () => host.Services.GetRequiredService<IOptions<DurableMessagingOptions>>().Value);
        exception.Message.ShouldContain("Messaging:Modules");
        OptionsValidationException rebusException = Should.Throw<OptionsValidationException>(
            () => host.Services.GetRequiredService<IOptions<RebusTransportOptions>>().Value);
        rebusException.Message.ShouldContain("Messaging:Rebus:Postgres:TransportSchema");
        rebusException.Message.ShouldContain("Messaging:Rebus:Postgres:TransportTable");
        rebusException.Message.ShouldContain("Messaging:Rebus:Postgres:SubscriptionTable");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddRebusTransport_WhenPersistenceRegistrationUsesUnknownModule_FailsOptionsValidation()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Messaging:Modules:0"] = "identity";

        builder.AddRebusTransport(transport =>
            transport.UsePostgresInternalTransport(builder.Configuration.GetSection("Messaging:Rebus")));
        builder.Services.AddModulePersistence<IdentityDbContext>("billing");
        using IHost host = builder.Build();

        OptionsValidationException exception = Should.Throw<OptionsValidationException>(
            () => host.Services.GetRequiredService<IOptions<DurableMessagingOptions>>().Value);
        exception.Message.ShouldContain("billing");
        exception.Message.ShouldContain("Messaging:Modules");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddRebusTransport_WhenMessageHandlerRegistrationUsesUnknownModule_FailsOptionsValidation()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Messaging:Modules:0"] = "identity";

        builder.AddRebusTransport(transport =>
            transport.UsePostgresInternalTransport(builder.Configuration.GetSection("Messaging:Rebus")));
        builder.Services.AddModuleMessaging("billing", typeof(TestModuleMessageHandler));
        using IHost host = builder.Build();

        OptionsValidationException exception = Should.Throw<OptionsValidationException>(
            () => host.Services.GetRequiredService<IOptions<DurableMessagingOptions>>().Value);
        exception.Message.ShouldContain("billing");
        exception.Message.ShouldContain("Messaging:Modules");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddModuleMessaging_WhenIntegrationEventHandlerIsRegistered_AddsEventSubscription()
    {
        var services = new ServiceCollection();

        services.AddModuleMessaging("identity", typeof(TestModuleMessageHandler));

        object subscription = services
            .Select(service => service.ImplementationInstance)
            .Where(instance => instance?.GetType().Name == "ModuleEventSubscription")
            .Single(instance => (Type)instance!.GetType().GetProperty("EventType")!.GetValue(instance)! == typeof(TestIntegrationEvent))!;
        subscription.GetType().GetProperty("ModuleName")!.GetValue(subscription)
            .ShouldBe("identity");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddModulePersistence_WhenMessageHandlerHasNoModulePersistence_FailsOptionsValidation()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Messaging:Modules:0"] = "identity";
        builder.Configuration["Messaging:Modules:1"] = "billing";

        builder.AddRebusTransport(transport =>
            transport.UsePostgresInternalTransport(builder.Configuration.GetSection("Messaging:Rebus")));
        builder.Services.AddModulePersistence<IdentityDbContext>("billing");
        builder.Services.AddModuleMessaging("identity", typeof(TestModuleMessageHandler));
        using IHost host = builder.Build();

        OptionsValidationException exception = Should.Throw<OptionsValidationException>(
            () => host.Services.GetRequiredService<IOptions<DurableMessagingOptions>>().Value);
        exception.Message.ShouldContain("identity");
        exception.Message.ShouldContain("module persistence");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddRebusTransport_WhenModulePersistenceHasMultipleDbContexts_FailsOptionsValidation()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Messaging:Modules:0"] = "identity";

        builder.AddRebusTransport(transport =>
            transport.UsePostgresInternalTransport(builder.Configuration.GetSection("Messaging:Rebus")));
        builder.Services.AddModulePersistence<IdentityDbContext>("identity");
        builder.Services.AddModulePersistence<AlternateIdentityDbContext>("identity");
        using IHost host = builder.Build();

        OptionsValidationException exception = Should.Throw<OptionsValidationException>(
            () => host.Services.GetRequiredService<IOptions<DurableMessagingOptions>>().Value);
        exception.Message.ShouldContain("identity");
        exception.Message.ShouldContain("multiple module persistence DbContexts");
        exception.Message.ShouldContain(nameof(IdentityDbContext));
        exception.Message.ShouldContain(nameof(AlternateIdentityDbContext));
    }

    [MessageIdentity("test.transport-configuration-event.v1")]
    private sealed record TestIntegrationEvent : IIntegrationEvent;

    private sealed class TestModuleMessageHandler : IModuleMessageHandler<TestIntegrationEvent>
    {
        public Task HandleAsync(TestIntegrationEvent message, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class AlternateIdentityDbContext(DbContextOptions<AlternateIdentityDbContext> options)
        : DbContext(options), IModuleDbContext
    {
        public string ModuleName => "identity";

        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

        public DbSet<StoredDomainEvent> DomainEvents => Set<StoredDomainEvent>();
    }

    private sealed class OperationsTransportDbContext(DbContextOptions<OperationsTransportDbContext> options)
        : DbContext(options), IModuleDbContext
    {
        public string ModuleName => "operations";

        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

        public DbSet<StoredDomainEvent> DomainEvents => Set<StoredDomainEvent>();
    }
}
