using Microsoft.EntityFrameworkCore;
using ModularTemplate.Operations.Operations;

namespace ModularTemplate.Operations.Infrastructure.Persistence;

public sealed class OperationRepository(IOperationsDbContext dbContext) : IOperationRepository
{
    public Task<Operation?> GetByIdAsync(Guid operationId, CancellationToken cancellationToken)
    {
        return dbContext.Operations.SingleOrDefaultAsync(x => x.Id == operationId, cancellationToken);
    }

    public void Add(Operation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        dbContext.Operations.Add(operation);
    }
}
