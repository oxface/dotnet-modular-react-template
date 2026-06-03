using Microsoft.EntityFrameworkCore;
using Bondstone.EntityFrameworkCore.Persistence;

namespace Bondstone.EntityFrameworkCore.Outbox;

public interface IOutboxClaimHandler
{
    Task MarkAbandonedProcessingMessagesAsync(
        IModuleDbContext dbContext,
        DateTimeOffset now,
        DateTimeOffset staleThreshold,
        CancellationToken cancellationToken);

    Task ClaimEligibleMessagesAsync(
        IModuleDbContext dbContext,
        string claimToken,
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken);
}
