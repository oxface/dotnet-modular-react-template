using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Bondstone;
using Bondstone.Messaging;
using Bondstone.Transport.Rebus;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Transport;
using Shouldly;

namespace ModularTemplate.Identity.Infrastructure.Tests.Persistence;

public sealed class ModuleScopedRebusHandlerTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenReceivingModuleHasNoMatchingHandler_Throws()
    {
        var message = new TestUnhandledEvent();
        using RebusTransactionScope transactionScope = StartRebusMessageContext(
            message,
            receivingModule: "products");

        await using ServiceProvider serviceProvider = new ServiceCollection().BuildServiceProvider();
        var handler = new ModuleScopedRebusHandler<TestUnhandledEvent>(
            serviceProvider,
            new ThrowingModuleMessageInbox(),
            [
                new ModuleMessageHandlerRegistration(
                    "identity",
                    typeof(TestUnhandledEvent),
                    typeof(UnusedHandler),
                    "test.unhandled-event.v1")
            ]);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await handler.Handle(message));
        exception.Message.ShouldContain("products");
        exception.Message.ShouldContain(typeof(TestUnhandledEvent).FullName!);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenReceivingModuleHasMultipleHandlersForMessage_Throws()
    {
        var message = new TestDurableCommand();
        using RebusTransactionScope transactionScope = StartRebusMessageContext(
            message,
            receivingModule: "products");

        await using ServiceProvider serviceProvider = new ServiceCollection().BuildServiceProvider();
        var handler = new ModuleScopedRebusHandler<TestDurableCommand>(
            serviceProvider,
            new ThrowingModuleMessageInbox(),
            [
                new ModuleMessageHandlerRegistration(
                    "products",
                    typeof(TestDurableCommand),
                    typeof(FirstCommandHandler),
                    "test.durable-command.v1"),
                new ModuleMessageHandlerRegistration(
                    "products",
                    typeof(TestDurableCommand),
                    typeof(SecondCommandHandler),
                    "test.durable-command.v1")
            ]);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await handler.Handle(message));
        exception.Message.ShouldContain("Multiple");
        exception.Message.ShouldContain("products");
        exception.Message.ShouldContain(typeof(TestDurableCommand).FullName!);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenIntegrationEventHasMultipleHandlers_RunsEachHandlerThroughSeparateInboxIdentity()
    {
        var message = new TestIntegrationEvent();
        using RebusTransactionScope transactionScope = StartRebusMessageContext(
            message,
            receivingModule: "products");

        await using ServiceProvider serviceProvider = new ServiceCollection()
            .AddScoped<FirstIntegrationEventHandler>()
            .AddScoped<SecondIntegrationEventHandler>()
            .BuildServiceProvider();
        var inbox = new CapturingModuleMessageInbox();
        var handler = new ModuleScopedRebusHandler<TestIntegrationEvent>(
            serviceProvider,
            inbox,
            [
                new ModuleMessageHandlerRegistration(
                    "products",
                    typeof(TestIntegrationEvent),
                    typeof(FirstIntegrationEventHandler),
                    "test.module-scoped-integration-event.v1",
                    "products.first-integration-handler.v1"),
                new ModuleMessageHandlerRegistration(
                    "products",
                    typeof(TestIntegrationEvent),
                    typeof(SecondIntegrationEventHandler),
                    "test.module-scoped-integration-event.v1",
                    "products.second-integration-handler.v1")
            ]);

        await handler.Handle(message);

        inbox.HandledIdentities.ShouldBe(
            [
                "products.first-integration-handler.v1",
                "products.second-integration-handler.v1"
            ]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenMessageHasTraceHeaders_StartsConsumerActivityForInboxAndHandler()
    {
        var message = new TestDurableCommand();
        Guid messageId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid durableOperationId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        using Activity parentActivity = new Activity("parent")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        using RebusTransactionScope transactionScope = StartRebusMessageContext(
            message,
            receivingModule: "products",
            messageId,
            parentActivity.Id,
            durableOperationId);
        await using ServiceProvider serviceProvider = new ServiceCollection()
            .AddScoped<HandledCommandHandler>()
            .BuildServiceProvider();
        var inbox = new CapturingModuleMessageInbox();
        var handler = new ModuleScopedRebusHandler<TestDurableCommand>(
            serviceProvider,
            inbox,
            [
                new ModuleMessageHandlerRegistration(
                    "products",
                    typeof(TestDurableCommand),
                    typeof(HandledCommandHandler),
                    "test.durable-command.v1")
            ]);

        await handler.Handle(message);

        inbox.TraceId.ShouldBe(parentActivity.TraceId.ToHexString());
        inbox.CausationId.ShouldBe(messageId);
        inbox.DurableOperationId.ShouldBe(durableOperationId);
    }

    private static RebusTransactionScope StartRebusMessageContext<TMessage>(
        TMessage message,
        string receivingModule,
        Guid? messageId = null,
        string? traceParent = null,
        Guid? durableOperationId = null)
    {
        var transactionScope = new RebusTransactionScope();
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Headers.MessageId] = (messageId ?? Guid.NewGuid()).ToString("D"),
            ["bondstone-receiving-module"] = receivingModule
        };
        if (!string.IsNullOrWhiteSpace(traceParent))
        {
            headers[BondstoneDiagnostics.TraceParentHeader] = traceParent;
        }

        if (durableOperationId is not null)
        {
            headers[BondstoneMessageHeaders.DurableOperationId] = durableOperationId.Value.ToString("D");
        }

        var transportMessage = new TransportMessage(headers, Array.Empty<byte>());
        var incomingStepContext = new IncomingStepContext(
            transportMessage,
            transactionScope.TransactionContext);
        incomingStepContext.Save(new Message(headers, message!));
        incomingStepContext.Save(CancellationToken.None);
        transactionScope.TransactionContext.Items[StepContext.StepContextKey] = incomingStepContext;

        return transactionScope;
    }

    [IntegrationEventIdentity("test.unhandled-event.v1")]
    private sealed record TestUnhandledEvent : IIntegrationEvent;

    [IntegrationEventIdentity("test.module-scoped-integration-event.v1")]
    private sealed record TestIntegrationEvent : IIntegrationEvent;

    [DurableCommandIdentity("test.durable-command.v1")]
    private sealed record TestDurableCommand : IDurableCommand;

    private sealed class UnusedHandler : IModuleMessageHandler<TestUnhandledEvent>
    {
        public Task HandleAsync(TestUnhandledEvent message, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FirstCommandHandler;

    private sealed class SecondCommandHandler;

    private sealed class HandledCommandHandler : IModuleMessageHandler<TestDurableCommand>
    {
        public Task HandleAsync(TestDurableCommand message, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    [IntegrationEventHandlerIdentity("products.first-integration-handler.v1")]
    private sealed class FirstIntegrationEventHandler : IModuleMessageHandler<TestIntegrationEvent>
    {
        public Task HandleAsync(TestIntegrationEvent message, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    [IntegrationEventHandlerIdentity("products.second-integration-handler.v1")]
    private sealed class SecondIntegrationEventHandler : IModuleMessageHandler<TestIntegrationEvent>
    {
        public Task HandleAsync(TestIntegrationEvent message, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingModuleMessageInbox : IModuleMessageInbox
    {
        public Task HandleOnceAsync(
            string moduleName,
            string messageId,
            string messageIdentity,
            Func<CancellationToken, Task> handler,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class CapturingModuleMessageInbox : IModuleMessageInbox
    {
        public string? TraceId { get; private set; }

        public Guid? CausationId { get; private set; }

        public Guid? DurableOperationId { get; private set; }

        public List<string> HandledIdentities { get; } = [];

        public async Task HandleOnceAsync(
            string moduleName,
            string messageId,
            string messageIdentity,
            Func<CancellationToken, Task> handler,
            CancellationToken cancellationToken)
        {
            TraceId = Activity.Current?.TraceId.ToHexString();
            CausationId = BondstoneDiagnostics.GetCurrentBaggageGuid(BondstoneDiagnostics.CausationIdBaggageKey);
            DurableOperationId = BondstoneDiagnostics.GetCurrentBaggageGuid(BondstoneDiagnostics.DurableOperationIdBaggageKey);
            HandledIdentities.Add(messageIdentity);
            await handler(cancellationToken);
        }
    }
}
