using ModularTemplate.Operations.Contracts.Operations;
using ModularTemplate.Operations.Operations.Events;
using ModularTemplate.SharedKernel.Domain;
using ModularTemplate.SharedKernel.Extensions;

namespace ModularTemplate.Operations.Operations;

public sealed class Operation : AggregateRoot<Guid>
{
    private Operation(
        Guid id,
        string operationType,
        string? metadataJson,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        OperationType = operationType.TrimRequired(nameof(operationType));
        Status = OperationStatus.Pending;
        MetadataJson = metadataJson;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;

        RaiseDomainEvent(new OperationCreatedDomainEvent(id, OperationType));
    }

    private Operation()
    {
    }

    public string OperationType { get; private set; } = string.Empty;

    public OperationStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public DateTimeOffset? FailedAtUtc { get; private set; }

    public string? FailureReason { get; private set; }

    public string? ResultJson { get; private set; }

    public string? MetadataJson { get; private set; }

    public static Operation Create(string operationType, string? metadataJson = null)
    {
        return new Operation(Guid.NewGuid(), operationType, metadataJson, DateTimeOffset.UtcNow);
    }

    public void MarkRunning()
    {
        EnsureNotCompleted(nameof(MarkRunning));
        EnsureNotFailed(nameof(MarkRunning));
        EnsureNotCancelled(nameof(MarkRunning));

        Status = OperationStatus.Running;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkCompleted(string? resultJson = null)
    {
        EnsureNotCompleted(nameof(MarkCompleted));
        EnsureNotCancelled(nameof(MarkCompleted));

        if (Status == OperationStatus.Failed)
        {
            throw new InvalidOperationException("Failed operations cannot transition to completed.");
        }

        Status = OperationStatus.Completed;
        ResultJson = resultJson;
        CompletedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CompletedAtUtc.Value;
        RaiseDomainEvent(new OperationCompletedDomainEvent(Id, OperationType));
    }

    public void MarkFailed(string reason)
    {
        EnsureNotCompleted(nameof(MarkFailed));
        EnsureNotFailed(nameof(MarkFailed));
        EnsureNotCancelled(nameof(MarkFailed));

        FailureReason = reason.TrimRequired(nameof(reason));
        Status = OperationStatus.Failed;
        FailedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = FailedAtUtc.Value;
        RaiseDomainEvent(new OperationFailedDomainEvent(Id, OperationType, FailureReason));
    }

    public void MarkCancelled()
    {
        EnsureNotCompleted(nameof(MarkCancelled));
        EnsureNotFailed(nameof(MarkCancelled));

        Status = OperationStatus.Cancelled;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private void EnsureNotCompleted(string operation)
    {
        if (Status == OperationStatus.Completed)
        {
            throw new InvalidOperationException($"Operation is already completed and cannot execute '{operation}'.");
        }
    }

    private void EnsureNotFailed(string operation)
    {
        if (Status == OperationStatus.Failed)
        {
            throw new InvalidOperationException($"Operation is already failed and cannot execute '{operation}'.");
        }
    }

    private void EnsureNotCancelled(string operation)
    {
        if (Status == OperationStatus.Cancelled)
        {
            throw new InvalidOperationException($"Operation is already cancelled and cannot execute '{operation}'.");
        }
    }
}
