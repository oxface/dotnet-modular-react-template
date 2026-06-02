using Bondstone.EntityFrameworkCore.Persistence;

namespace Bondstone.EntityFrameworkCore.Inbox;

public interface IInboxMessageProcessor
{
    Task<InboxMessage?> ClaimAsync(
        IModuleDbContext dbContext,
        string messageId,
        string moduleName,
        string handlerName,
        CancellationToken cancellationToken);
}
