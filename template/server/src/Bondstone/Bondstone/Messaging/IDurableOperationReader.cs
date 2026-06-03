namespace Bondstone.Messaging;

public interface IDurableOperationReader
{
    Task<DurableOperationSnapshot?> GetOperationAsync(
        Guid durableOperationId,
        CancellationToken cancellationToken);
}
