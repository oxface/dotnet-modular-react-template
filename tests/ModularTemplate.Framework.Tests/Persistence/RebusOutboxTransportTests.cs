using System.Diagnostics;
using Bondstone.EntityFrameworkCore.Outbox;
using Bondstone.Messaging;
using Bondstone.Transport.Rebus;
using NSubstitute;
using Rebus.Bus;
using Rebus.Bus.Advanced;
using Rebus.ServiceProvider;
using Shouldly;

namespace ModularTemplate.Identity.Infrastructure.Tests.Persistence;

public sealed class RebusOutboxTransportTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task DispatchAsync_WhenCommandIsDispatched_SendsToResolvedDestination()
    {
        var registry = new MessageTypeRegistry();
        registry.Register<TestCommand>();
        IBus bus = Substitute.For<IBus>();
        IAdvancedApi advanced = Substitute.For<IAdvancedApi>();
        IRoutingApi routing = Substitute.For<IRoutingApi>();
        bus.Advanced.Returns(advanced);
        advanced.Routing.Returns(routing);
        IBusRegistry busRegistry = Substitute.For<IBusRegistry>();
        busRegistry.GetBus("identity:queue").Returns(bus);
        IOutboxRouteResolver routeResolver = Substitute.For<IOutboxRouteResolver>();
        routeResolver.Resolve(Arg.Any<OutboxMessage>())
            .Returns(new OutboxRoute("identity:queue", "modular-template.products"));
        var transport = new RebusOutboxTransport(busRegistry, registry, routeResolver);
        using Activity activity = new Activity("test outbox dispatch")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        OutboxMessage outboxMessage = OutboxMessage.Create(
            Guid.NewGuid(),
            MessageKind.Command,
            "test.command.v1",
            sourceModule: "identity",
            targetModule: "products",
            correlationId: Guid.NewGuid(),
            causationId: null,
            durableOperationId: null,
            payload: "{\"Value\":\"hello\"}",
            partitionKey: "identity:user-1",
            metadata: MessageTraceContext.CaptureMetadata());

        await transport.DispatchAsync(outboxMessage, CancellationToken.None);

        await routing.Received(1).Send(
            "modular-template.products",
            Arg.Is<TestCommand>(message => message.Value == "hello"),
            Arg.Is<Dictionary<string, string>>(headers =>
                headers[BondstoneMessageHeaders.MessageType] == "test.command.v1"
                && headers[BondstoneMessageHeaders.SourceModule] == "identity"
                && headers[BondstoneMessageHeaders.TargetModule] == "products"
                && headers[BondstoneMessageHeaders.PartitionKey] == "identity:user-1"
                && headers[BondstoneDiagnostics.TraceParentHeader] == activity.Id));
        await bus.DidNotReceiveWithAnyArgs().Publish(default!);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DispatchAsync_WhenEventIsDispatched_PublishesThroughResolvedBus()
    {
        var registry = new MessageTypeRegistry();
        registry.Register<TestEvent>();
        IBus bus = Substitute.For<IBus>();
        IBusRegistry busRegistry = Substitute.For<IBusRegistry>();
        busRegistry.GetBus("identity:queue").Returns(bus);
        IOutboxRouteResolver routeResolver = Substitute.For<IOutboxRouteResolver>();
        routeResolver.Resolve(Arg.Any<OutboxMessage>())
            .Returns(new OutboxRoute("identity:queue", DestinationAddress: null));
        var transport = new RebusOutboxTransport(busRegistry, registry, routeResolver);
        OutboxMessage outboxMessage = OutboxMessage.Create(
            Guid.NewGuid(),
            MessageKind.Event,
            "test.event.v1",
            sourceModule: "identity",
            targetModule: null,
            correlationId: Guid.NewGuid(),
            causationId: null,
            durableOperationId: null,
            payload: "{\"Value\":\"world\"}");

        await transport.DispatchAsync(outboxMessage, CancellationToken.None);

        await bus.Received(1).Publish(
            Arg.Is<TestEvent>(message => message.Value == "world"),
            Arg.Is<Dictionary<string, string>>(headers =>
                headers[BondstoneMessageHeaders.MessageType] == "test.event.v1"
                && !headers.ContainsKey(BondstoneMessageHeaders.TargetModule)));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_WhenCommandTargetsModule_UsesSourceBusAndTargetQueue()
    {
        var resolver = new OutboxRouteResolver(
            [new ModuleTopologyRegistration("identity"), new ModuleTopologyRegistration("products")],
            Microsoft.Extensions.Options.Options.Create(new RebusTransportOptions { QueuePrefix = "sample" }));
        OutboxMessage message = OutboxMessage.Create(
            Guid.NewGuid(),
            MessageKind.Command,
            "test.command.v1",
            sourceModule: "identity",
            targetModule: "products",
            correlationId: Guid.NewGuid(),
            causationId: null,
            durableOperationId: null,
            payload: "{}");

        OutboxRoute route = resolver.Resolve(message);

        route.BusKey.ShouldBe("identity:queue");
        route.DestinationAddress.ShouldBe("sample.products");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_WhenCommandTargetsUnknownModule_Throws()
    {
        var resolver = new OutboxRouteResolver(
            [new ModuleTopologyRegistration("identity")],
            Microsoft.Extensions.Options.Options.Create(new RebusTransportOptions { QueuePrefix = "sample" }));
        OutboxMessage message = OutboxMessage.Create(
            Guid.NewGuid(),
            MessageKind.Command,
            "test.command.v1",
            sourceModule: "identity",
            targetModule: "products",
            correlationId: Guid.NewGuid(),
            causationId: null,
            durableOperationId: null,
            payload: "{}");

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => resolver.Resolve(message));

        exception.Message.ShouldContain("products");
        exception.Message.ShouldContain("registered Bondstone module");
    }

    [DurableCommandIdentity("test.command.v1")]
    private sealed record TestCommand(string Value) : IDurableCommand;

    [IntegrationEventIdentity("test.event.v1")]
    private sealed record TestEvent(string Value) : IIntegrationEvent;
}
