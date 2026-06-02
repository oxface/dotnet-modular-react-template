using Microsoft.EntityFrameworkCore;
using Bondstone.EntityFrameworkCore.Persistence;

namespace Bondstone.EntityFrameworkCore.Inbox;

public sealed class InboxMessageProcessor(
    IInboxClaimConflictDetector claimConflictDetector)
    : IInboxMessageProcessor
{
    public async Task<InboxMessage?> ClaimAsync(
        IModuleDbContext dbContext,
        string messageId,
        string moduleName,
        string handlerName,
        CancellationToken cancellationToken)
    {
        InboxMessage? inboxMessage = await FindInboxMessageAsync(
            dbContext,
            messageId,
            moduleName,
            handlerName,
            cancellationToken);

        if (inboxMessage?.IsProcessed == true)
        {
            return null;
        }

        if (inboxMessage is null)
        {
            inboxMessage = InboxMessage.Create(messageId, moduleName, handlerName);
            dbContext.InboxMessages.Add(inboxMessage);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (claimConflictDetector.IsClaimConflict(ex))
            {
                DetachInboxMessage(dbContext, inboxMessage);

                InboxMessage? competingInboxMessage = await FindInboxMessageAsync(
                    dbContext,
                    messageId,
                    moduleName,
                    handlerName,
                    cancellationToken);

                if (competingInboxMessage?.IsProcessed == true)
                {
                    return null;
                }

                throw;
            }
        }

        return inboxMessage;
    }

    private static Task<InboxMessage?> FindInboxMessageAsync(
        IModuleDbContext dbContext,
        string messageId,
        string moduleName,
        string handlerName,
        CancellationToken cancellationToken)
    {
        return dbContext.InboxMessages
            .SingleOrDefaultAsync(
                x => x.MessageId == messageId
                    && x.ModuleName == moduleName
                    && x.HandlerName == handlerName,
                cancellationToken);
    }

    private static void DetachInboxMessage(IModuleDbContext dbContext, InboxMessage inboxMessage)
    {
        foreach (var entry in dbContext.ChangeTracker.Entries<InboxMessage>())
        {
            if (ReferenceEquals(entry.Entity, inboxMessage))
            {
                entry.State = EntityState.Detached;
                return;
            }
        }
    }

}
