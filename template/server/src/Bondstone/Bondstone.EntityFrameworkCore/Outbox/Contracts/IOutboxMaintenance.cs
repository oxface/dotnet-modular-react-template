namespace Bondstone.EntityFrameworkCore.Outbox;

public interface IOutboxMaintenance
{
    string ModuleName { get; }

    Task<bool> RequeueDeadLetteredAsync(
        Guid messageId,
        CancellationToken cancellationToken = default);

    Task<int> DeleteProcessedBeforeAsync(
        DateTimeOffset cutoffUtc,
        CancellationToken cancellationToken = default);
}
