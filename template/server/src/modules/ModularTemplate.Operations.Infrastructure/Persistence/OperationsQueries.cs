using Microsoft.EntityFrameworkCore;
using ModularTemplate.Operations.Contracts.Operations;

namespace ModularTemplate.Operations.Infrastructure.Persistence;

public sealed class OperationsQueries(IOperationsDbContext dbContext) : IOperationsQueries
{
    public async Task<OperationDetails?> GetOperationAsync(Guid operationId, CancellationToken cancellationToken)
    {
        return await dbContext.Operations
            .AsNoTracking()
            .Where(x => x.Id == operationId)
            .Select(x => new OperationDetails(
                x.Id,
                x.OperationType,
                x.Status,
                x.CreatedAtUtc,
                x.UpdatedAtUtc,
                x.CompletedAtUtc,
                x.FailedAtUtc,
                x.FailureReason,
                x.ResultJson,
                x.MetadataJson))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
