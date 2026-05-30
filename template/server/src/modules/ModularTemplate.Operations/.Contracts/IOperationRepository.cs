using ModularTemplate.Operations.Operations;

namespace ModularTemplate.Operations;

public interface IOperationRepository
{
    Task<Operation?> GetByIdAsync(Guid operationId, CancellationToken cancellationToken);

    void Add(Operation operation);
}
