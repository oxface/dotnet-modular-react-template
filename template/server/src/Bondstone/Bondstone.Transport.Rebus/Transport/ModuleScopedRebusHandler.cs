using Microsoft.Extensions.DependencyInjection;
using Bondstone.Messaging;
using Rebus.Extensions;
using Rebus.Handlers;
using Rebus.Messages;
using Rebus.Pipeline;

namespace Bondstone.Transport.Rebus;

public sealed class ModuleScopedRebusHandler<TMessage>(
    IServiceProvider serviceProvider,
    IModuleMessageInbox moduleMessageInbox,
    IEnumerable<ModuleMessageHandlerRegistration> registrations)
    : IHandleMessages<TMessage>
{
    public async Task Handle(TMessage message)
    {
        string moduleName = GetCurrentReceivingModule();

        ModuleMessageHandlerRegistration[] handlers = registrations
            .Where(registration => string.Equals(registration.ModuleName, moduleName, StringComparison.Ordinal)
                && registration.MessageType == typeof(TMessage))
            .ToArray();

        if (handlers.Length == 0)
        {
            throw new InvalidOperationException(
                $"No module message handler is registered for receiving module '{moduleName}' " +
                $"and message type '{typeof(TMessage).FullName}'.");
        }

        if (handlers.Length > 1)
        {
            if (!typeof(IIntegrationEvent).IsAssignableFrom(typeof(TMessage)))
            {
                throw new InvalidOperationException(
                    $"Multiple module message handlers are registered for durable command '{typeof(TMessage).FullName}' " +
                    $"in receiving module '{moduleName}'. Durable commands must have exactly one target-module handler.");
            }
        }

        CancellationToken cancellationToken = GetCurrentCancellationToken();
        string messageId = GetCurrentMessageId();
        IReadOnlyDictionary<string, string> headers = GetCurrentHeaders();

        using var activity = RebusMessageDiagnostics.StartHandlingActivity<TMessage>(
            moduleName,
            messageId,
            GetActivityMessageIdentity(handlers),
            headers);

        foreach (ModuleMessageHandlerRegistration handler in handlers)
        {
            await moduleMessageInbox.HandleOnceAsync(
                moduleName,
                messageId,
                handler.HandlerIdentity,
                ct => HandleAsync(handler, message, ct),
                cancellationToken);
        }
    }

    private async Task HandleAsync(
        ModuleMessageHandlerRegistration registration,
        TMessage message,
        CancellationToken cancellationToken)
    {
        var handler = (IModuleMessageHandler<TMessage>)serviceProvider.GetRequiredService(registration.HandlerType);
        await handler.HandleAsync(message, cancellationToken);
    }

    private static string GetActivityMessageIdentity(IReadOnlyCollection<ModuleMessageHandlerRegistration> handlers)
    {
        string[] messageIdentities = handlers
            .Select(handler => handler.MessageIdentity)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return messageIdentities.Length == 1
            ? messageIdentities[0]
            : typeof(TMessage).FullName ?? typeof(TMessage).Name;
    }

    private static string GetCurrentMessageId()
    {
        IReadOnlyDictionary<string, string> headers = MessageContext.Current?.Headers
            ?? throw new InvalidOperationException("No Rebus message context is active.");

        if (headers.TryGetValue(BondstoneMessageHeaders.MessageId, out string? stableMessageId)
            && !string.IsNullOrWhiteSpace(stableMessageId))
        {
            return stableMessageId;
        }

        if (headers.TryGetValue(Headers.MessageId, out string? rebusMessageId)
            && !string.IsNullOrWhiteSpace(rebusMessageId))
        {
            return rebusMessageId;
        }

        throw new InvalidOperationException("The incoming message does not include a message id header.");
    }

    private static string GetCurrentReceivingModule()
    {
        IReadOnlyDictionary<string, string> headers = MessageContext.Current?.Headers
            ?? throw new InvalidOperationException("No Rebus message context is active.");

        if (headers.TryGetValue(RebusMessageHeaders.ReceivingModule, out string? moduleName)
            && !string.IsNullOrWhiteSpace(moduleName))
        {
            return moduleName;
        }

        throw new InvalidOperationException(
            $"The incoming message does not include the {RebusMessageHeaders.ReceivingModule} receive header.");
    }

    private static CancellationToken GetCurrentCancellationToken()
    {
        IMessageContext messageContext = MessageContext.Current
            ?? throw new InvalidOperationException("No Rebus message context is active.");

        return messageContext.GetCancellationToken();
    }

    private static IReadOnlyDictionary<string, string> GetCurrentHeaders()
    {
        return MessageContext.Current?.Headers
            ?? throw new InvalidOperationException("No Rebus message context is active.");
    }
}
