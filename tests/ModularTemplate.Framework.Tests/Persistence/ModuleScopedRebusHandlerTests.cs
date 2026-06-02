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
            receivingModule: "operations");

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
        exception.Message.ShouldContain("operations");
        exception.Message.ShouldContain(typeof(TestUnhandledEvent).FullName!);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenReceivingModuleHasMultipleHandlersForMessage_Throws()
    {
        var message = new TestDurableCommand();
        using RebusTransactionScope transactionScope = StartRebusMessageContext(
            message,
            receivingModule: "operations");

        await using ServiceProvider serviceProvider = new ServiceCollection().BuildServiceProvider();
        var handler = new ModuleScopedRebusHandler<TestDurableCommand>(
            serviceProvider,
            new ThrowingModuleMessageInbox(),
            [
                new ModuleMessageHandlerRegistration(
                    "operations",
                    typeof(TestDurableCommand),
                    typeof(FirstCommandHandler),
                    "test.durable-command.v1"),
                new ModuleMessageHandlerRegistration(
                    "operations",
                    typeof(TestDurableCommand),
                    typeof(SecondCommandHandler),
                    "test.durable-command.v1")
            ]);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await handler.Handle(message));
        exception.Message.ShouldContain("Multiple");
        exception.Message.ShouldContain("operations");
        exception.Message.ShouldContain(typeof(TestDurableCommand).FullName!);
    }

    private static RebusTransactionScope StartRebusMessageContext<TMessage>(
        TMessage message,
        string receivingModule)
    {
        var transactionScope = new RebusTransactionScope();
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Headers.MessageId] = Guid.NewGuid().ToString("D"),
            ["modular-template-receiving-module"] = receivingModule
        };
        var transportMessage = new TransportMessage(headers, Array.Empty<byte>());
        var incomingStepContext = new IncomingStepContext(
            transportMessage,
            transactionScope.TransactionContext);
        incomingStepContext.Save(new Message(headers, message!));
        transactionScope.TransactionContext.Items[StepContext.StepContextKey] = incomingStepContext;

        return transactionScope;
    }

    [MessageIdentity("test.unhandled-event.v1")]
    private sealed record TestUnhandledEvent : IIntegrationEvent;

    [MessageIdentity("test.durable-command.v1")]
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
}
