using Bondstone.EntityFrameworkCore.Persistence;
using Bondstone.Messaging;

namespace Bondstone.EntityFrameworkCore.Inbox;

public sealed class EntityFrameworkCoreModuleMessageInbox(
    IModuleBoundary moduleBoundary,
    IInboxMessageProcessor inboxMessageProcessor,
    IModulePersistenceResolver persistenceResolver)
    : IModuleMessageInbox
{
    public async Task HandleOnceAsync(
        string moduleName,
        string messageId,
        string messageIdentity,
        Func<CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageIdentity);
        ArgumentNullException.ThrowIfNull(handler);

        await moduleBoundary.ExecuteAsync(
            moduleName,
            async ct =>
            {
                IModuleDbContext dbContext = persistenceResolver.ResolveDbContext(moduleName);
                InboxMessage? inboxMessage = await inboxMessageProcessor.ClaimAsync(
                    dbContext,
                    messageId,
                    moduleName,
                    messageIdentity,
                    ct);

                if (inboxMessage is null)
                {
                    return;
                }

                await handler(ct);
                inboxMessage.MarkProcessed();
            },
            cancellationToken);
    }
}
