using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ModularTemplate.Identity.Infrastructure.Persistence;
using ModularTemplate.Infrastructure.Inbox;
using ModularTemplate.Infrastructure.Outbox;
using ModularTemplate.Infrastructure.Persistence;
using ModularTemplate.Infrastructure.Persistence.DomainEvents;
using ModularTemplate.Infrastructure.Transport;
using ModularTemplate.SharedKernel.Messaging;
using Rebus.ServiceProvider;
using Shouldly;

namespace ModularTemplate.Identity.Infrastructure.Tests.Persistence;

public sealed class TransportConfigurationTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void AddTransport_WhenMessagingIsDisabled_DoesNotRequireRebusRuntimeServices()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Messaging:Enabled"] = "false";

        builder.AddTransport();

        builder.Services.Any(service => service.ServiceType == typeof(IOutboxTransport)).ShouldBeFalse();
        builder.Services.Any(service => service.ServiceType == typeof(IOutboxDispatchLock)).ShouldBeFalse();
        builder.Services.Any(service => service.ServiceType == typeof(IOutboxDispatcher)).ShouldBeFalse();
        builder.Services.Any(service => service.ServiceType == typeof(IBusRegistry)).ShouldBeFalse();

        using IHost host = builder.Build();
        host.Services.GetServices<IHostedService>()
            .Any(service => service.GetType() == typeof(OutboxDispatcherBackgroundService))
            .ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddTransport_WhenMessagingIsEnabled_RegistersPostgresOutboxDispatchLock()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Messaging:Enabled"] = "true";
        builder.Configuration["Messaging:Modules:0"] = "identity";

        builder.AddTransport();

        builder.Services.Any(service =>
            service.ServiceType == typeof(IOutboxDispatchLock)
            && service.ImplementationType == typeof(PostgresAdvisoryOutboxDispatchLock))
            .ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddTransport_WhenMessagingIsDisabled_DoesNotRequireMessagingTopologyOptions()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Messaging:Enabled"] = "false";
        builder.Configuration["Messaging:Modules:0"] = "";
        builder.Configuration["Messaging:QueuePrefix"] = "";
        builder.Configuration["Messaging:ConnectionStringName"] = "";
        builder.Configuration["Messaging:TransportSchema"] = "";
        builder.Configuration["Messaging:TransportTable"] = "";
        builder.Configuration["Messaging:SubscriptionTable"] = "";
        builder.Configuration["Messaging:PollingInterval"] = "00:00:00";
        builder.Configuration["Messaging:BatchSize"] = "0";
        builder.Configuration["Messaging:MaxAttempts"] = "0";
        builder.Configuration["Messaging:LockTimeout"] = "00:00:00";

        builder.AddTransport();
        using IHost host = builder.Build();

        host.Services.GetRequiredService<IOptions<DurableMessagingOptions>>()
            .Value
            .Enabled
            .ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddTransport_WhenMessagingIsEnabledAndTopologyOptionsAreInvalid_FailsOptionsValidation()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Messaging:Enabled"] = "true";
        builder.Configuration["Messaging:Modules:0"] = "";
        builder.Configuration["Messaging:QueuePrefix"] = "";
        builder.Configuration["Messaging:PollingInterval"] = "00:00:00";
        builder.Configuration["Messaging:RetryDelays:0"] = "-00:00:01";

        builder.AddTransport();
        using IHost host = builder.Build();

        OptionsValidationException exception = Should.Throw<OptionsValidationException>(
            () => host.Services.GetRequiredService<IOptions<DurableMessagingOptions>>().Value);
        exception.Message.ShouldContain("Messaging:QueuePrefix");
        exception.Message.ShouldContain("Messaging:PollingInterval");
        exception.Message.ShouldContain("Messaging:RetryDelays");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddTransport_WhenMessagingSqlIdentifiersAreInvalid_FailsOptionsValidation()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Messaging:Enabled"] = "true";
        builder.Configuration["Messaging:Modules:0"] = "identity-module";
        builder.Configuration["Messaging:TransportSchema"] = "transport-schema";
        builder.Configuration["Messaging:TransportTable"] = "rebus messages";
        builder.Configuration["Messaging:SubscriptionTable"] = "rebus-subscriptions";

        builder.AddTransport();
        using IHost host = builder.Build();

        OptionsValidationException exception = Should.Throw<OptionsValidationException>(
            () => host.Services.GetRequiredService<IOptions<DurableMessagingOptions>>().Value);
        exception.Message.ShouldContain("Messaging:Modules");
        exception.Message.ShouldContain("Messaging:TransportSchema");
        exception.Message.ShouldContain("Messaging:TransportTable");
        exception.Message.ShouldContain("Messaging:SubscriptionTable");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddTransport_WhenPersistenceRegistrationUsesUnknownModule_FailsOptionsValidation()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Messaging:Modules:0"] = "identity";

        builder.AddTransport();
        builder.Services.AddModulePersistence<IdentityDbContext>("billing");
        using IHost host = builder.Build();

        OptionsValidationException exception = Should.Throw<OptionsValidationException>(
            () => host.Services.GetRequiredService<IOptions<DurableMessagingOptions>>().Value);
        exception.Message.ShouldContain("billing");
        exception.Message.ShouldContain("Messaging:Modules");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddTransport_WhenMessageHandlerRegistrationUsesUnknownModule_FailsOptionsValidation()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Messaging:Modules:0"] = "identity";

        builder.AddTransport();
        builder.Services.AddModuleMessaging("billing", typeof(TestModuleMessageHandler));
        using IHost host = builder.Build();

        OptionsValidationException exception = Should.Throw<OptionsValidationException>(
            () => host.Services.GetRequiredService<IOptions<DurableMessagingOptions>>().Value);
        exception.Message.ShouldContain("billing");
        exception.Message.ShouldContain("Messaging:Modules");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddTransport_WhenEventSubscriptionHasNoModuleHandler_FailsOptionsValidation()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Messaging:Modules:0"] = "identity";

        builder.AddTransport();
        builder.Services.AddModuleEventSubscriptions("identity", typeof(TestIntegrationEvent));
        using IHost host = builder.Build();

        OptionsValidationException exception = Should.Throw<OptionsValidationException>(
            () => host.Services.GetRequiredService<IOptions<DurableMessagingOptions>>().Value);
        exception.Message.ShouldContain("identity");
        exception.Message.ShouldContain(typeof(TestIntegrationEvent).FullName!);
        exception.Message.ShouldContain("matching module message handler");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddTransport_WhenMessageHandlerHasNoModulePersistence_FailsOptionsValidation()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Messaging:Modules:0"] = "identity";

        builder.AddTransport();
        builder.Services.AddModuleMessaging("identity", typeof(TestModuleMessageHandler));
        using IHost host = builder.Build();

        OptionsValidationException exception = Should.Throw<OptionsValidationException>(
            () => host.Services.GetRequiredService<IOptions<DurableMessagingOptions>>().Value);
        exception.Message.ShouldContain("identity");
        exception.Message.ShouldContain("module persistence");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddTransport_WhenModulePersistenceHasMultipleDbContexts_FailsOptionsValidation()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Messaging:Modules:0"] = "identity";

        builder.AddTransport();
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
}
