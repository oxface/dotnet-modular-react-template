namespace ModularTemplate.Operations.Contracts.Operations;

public interface IOperationsQueries
{
    Task<OperationDetails?> GetOperationAsync(Guid operationId, CancellationToken cancellationToken);
}
