using Microsoft.EntityFrameworkCore;
using Bondstone.EntityFrameworkCore.Persistence;
using Bondstone.Messaging;
using Bondstone.Internal;

namespace Bondstone.EntityFrameworkCore.Inbox;

public sealed class EntityFrameworkCoreModuleMessageInbox(
    IEnumerable<IModuleMessageInboxExecutor> executors)
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

        string normalizedModuleName = moduleName.TrimRequired(nameof(moduleName));
        IModuleMessageInboxExecutor executor = executors.SingleOrDefault(executor =>
                string.Equals(executor.ModuleName, normalizedModuleName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"No module message inbox executor exists for module '{normalizedModuleName}'.");

        await executor.HandleOnceAsync(
            messageId,
            messageIdentity,
            handler,
            cancellationToken);
    }
}

public interface IModuleMessageInboxExecutor
{
    string ModuleName { get; }

    Task HandleOnceAsync(
        string messageId,
        string messageIdentity,
        Func<CancellationToken, Task> handler,
        CancellationToken cancellationToken);
}

public sealed class EntityFrameworkCoreModuleMessageInbox<TDbContext>(
    TDbContext dbContext,
    ModuleUnitOfWork<TDbContext> unitOfWork,
    IInboxMessageProcessor inboxMessageProcessor)
    : IModuleMessageInboxExecutor
    where TDbContext : DbContext, IModuleDbContext
{
    public string ModuleName => dbContext.ModuleName;

    public async Task HandleOnceAsync(
        string messageId,
        string messageIdentity,
        Func<CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        await unitOfWork.ExecuteTransactionalAsync(
            async ct =>
            {
                InboxMessage? inboxMessage = await inboxMessageProcessor.ClaimAsync(
                    dbContext,
                    messageId,
                    dbContext.ModuleName,
                    messageIdentity,
                    ct);

                if (inboxMessage is null)
                {
                    return true;
                }

                await handler(ct);
                inboxMessage.MarkProcessed();
                return true;
            },
            cancellationToken);
    }
}
