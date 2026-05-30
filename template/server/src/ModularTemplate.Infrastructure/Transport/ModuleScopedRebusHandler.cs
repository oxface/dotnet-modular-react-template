using Microsoft.Extensions.DependencyInjection;
using ModularTemplate.Infrastructure.Inbox;
using ModularTemplate.Infrastructure.Persistence;
using ModularTemplate.SharedKernel.Messaging;
using Rebus.Extensions;
using Rebus.Handlers;
using Rebus.Messages;
using Rebus.Pipeline;

namespace ModularTemplate.Infrastructure.Transport;

public sealed class ModuleScopedRebusHandler<TMessage>(
    IServiceProvider serviceProvider,
    IInboxMessageProcessor inboxMessageProcessor,
    IEnumerable<ModuleMessageHandlerRegistration> registrations,
    IEnumerable<IModuleUnitOfWork> unitOfWorks,
    IEnumerable<IModuleDbContext> moduleContexts)
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
            throw new InvalidOperationException(
                $"Multiple module message handlers are registered for message '{typeof(TMessage).FullName}' " +
                $"in receiving module '{moduleName}'. Use one module message handler per message identity.");
        }

        CancellationToken cancellationToken = GetCurrentCancellationToken();
        IModuleUnitOfWork unitOfWork = ResolveModuleUnitOfWork(moduleName);
        IModuleDbContext dbContext = ResolveModuleDbContext(moduleName);

        foreach (ModuleMessageHandlerRegistration handler in handlers)
        {
            await unitOfWork.ExecuteTransactionalAsync(
                async cancellationToken =>
                {
                    await HandleOnceAsync(dbContext, handler, message, cancellationToken);
                    return true;
                },
                cancellationToken);
        }
    }

    private async Task HandleOnceAsync(
        IModuleDbContext dbContext,
        ModuleMessageHandlerRegistration registration,
        TMessage message,
        CancellationToken cancellationToken)
    {
        string messageId = GetCurrentMessageId();

        InboxMessage? inboxMessage = await inboxMessageProcessor.ClaimAsync(
            dbContext,
            messageId,
            registration.ModuleName,
            registration.MessageIdentity,
            cancellationToken);

        if (inboxMessage is null)
        {
            return;
        }

        var handler = (IModuleMessageHandler<TMessage>)serviceProvider.GetRequiredService(registration.HandlerType);
        await handler.HandleAsync(message, cancellationToken);
        inboxMessage.MarkProcessed();
    }

    private IModuleUnitOfWork ResolveModuleUnitOfWork(string moduleName)
    {
        IModuleUnitOfWork[] matchingUnitOfWorks = unitOfWorks
            .Where(unitOfWork => string.Equals(unitOfWork.ModuleName, moduleName, StringComparison.Ordinal))
            .ToArray();

        return matchingUnitOfWorks.Length switch
        {
            1 => matchingUnitOfWorks[0],
            0 => throw new InvalidOperationException(
                $"No module unit of work is registered for receiving module '{moduleName}'."),
            _ => throw new InvalidOperationException(
                $"Multiple module unit of work instances are registered for receiving module '{moduleName}'."),
        };
    }

    private IModuleDbContext ResolveModuleDbContext(string moduleName)
    {
        IModuleDbContext[] matchingContexts = moduleContexts
            .Where(dbContext => string.Equals(dbContext.ModuleName, moduleName, StringComparison.Ordinal))
            .ToArray();

        return matchingContexts.Length switch
        {
            1 => matchingContexts[0],
            0 => throw new InvalidOperationException(
                $"No module DbContext is registered for receiving module '{moduleName}'."),
            _ => throw new InvalidOperationException(
                $"Multiple module DbContexts are registered for receiving module '{moduleName}'."),
        };
    }

    private static string GetCurrentMessageId()
    {
        IReadOnlyDictionary<string, string> headers = MessageContext.Current?.Headers
            ?? throw new InvalidOperationException("No Rebus message context is active.");

        if (headers.TryGetValue(RebusMessageHeaders.MessageId, out string? stableMessageId)
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
}
