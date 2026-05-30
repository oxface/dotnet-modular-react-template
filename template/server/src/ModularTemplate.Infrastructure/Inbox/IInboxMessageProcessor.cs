using ModularTemplate.Infrastructure.Persistence;

namespace ModularTemplate.Infrastructure.Inbox;

public interface IInboxMessageProcessor
{
    Task<InboxMessage?> ClaimAsync(
        IModuleDbContext dbContext,
        string messageId,
        string moduleName,
        string handlerName,
        CancellationToken cancellationToken);
}
