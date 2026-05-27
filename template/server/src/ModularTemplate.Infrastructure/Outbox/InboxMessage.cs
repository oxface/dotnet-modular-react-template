using ModularTemplate.SharedKernel.Messaging;

namespace ModularTemplate.Infrastructure.Outbox;

public sealed class InboxMessage
{
    private InboxMessage(
        Guid id,
        Guid messageId,
        MessageKind messageKind,
        string messageType,
        string sourceModule,
        string targetModule,
        Guid correlationId,
        Guid? causationId,
        Guid? operationId,
        string? idempotencyKey,
        string payload,
        string? metadata,
        DateTimeOffset receivedAtUtc,
        int maxAttempts)
    {
        Id = id;
        MessageId = messageId;
        MessageKind = messageKind;
        MessageType = messageType;
        SourceModule = sourceModule;
        TargetModule = targetModule;
        CorrelationId = correlationId;
        CausationId = causationId;
        OperationId = operationId;
        IdempotencyKey = idempotencyKey;
        Payload = payload;
        Metadata = metadata;
        Status = PersistedMessageStatus.Pending;
        AttemptCount = 0;
        MaxAttempts = maxAttempts;
        NextAttemptAtUtc = receivedAtUtc;
        ReceivedAtUtc = receivedAtUtc;
    }

    private InboxMessage()
    {
    }

    public Guid Id { get; private set; }

    public Guid MessageId { get; private set; }

    public MessageKind MessageKind { get; private set; }

    public string MessageType { get; private set; } = string.Empty;

    public string SourceModule { get; private set; } = string.Empty;

    public string TargetModule { get; private set; } = string.Empty;

    public Guid CorrelationId { get; private set; }

    public Guid? CausationId { get; private set; }

    public Guid? OperationId { get; private set; }

    public string? IdempotencyKey { get; private set; }

    public string Payload { get; private set; } = string.Empty;

    public string? Metadata { get; private set; }

    public PersistedMessageStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public int MaxAttempts { get; private set; }

    public DateTimeOffset NextAttemptAtUtc { get; private set; }

    public DateTimeOffset ReceivedAtUtc { get; private set; }

    public DateTimeOffset? LockedAtUtc { get; private set; }

    public string? LockedBy { get; private set; }

    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    public DateTimeOffset? FailedAtUtc { get; private set; }

    public string? Error { get; private set; }

    public static InboxMessage Create(
        Guid messageId,
        MessageKind messageKind,
        string messageType,
        string sourceModule,
        string targetModule,
        Guid correlationId,
        Guid? causationId,
        Guid? operationId,
        string? idempotencyKey,
        string payload,
        string? metadata = null,
        int maxAttempts = 5)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceModule);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetModule);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "Max attempts must be greater than zero.");
        }

        return new InboxMessage(
            Guid.NewGuid(),
            messageId,
            messageKind,
            messageType.Trim(),
            sourceModule.Trim(),
            targetModule.Trim(),
            correlationId,
            causationId,
            operationId,
            idempotencyKey?.Trim(),
            payload,
            metadata,
            DateTimeOffset.UtcNow,
            maxAttempts);
    }

    public void MarkProcessing(string lockedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockedBy);

        Status = PersistedMessageStatus.Processing;
        LockedAtUtc = DateTimeOffset.UtcNow;
        LockedBy = lockedBy.Trim();
        Error = null;
    }

    public void MarkProcessed()
    {
        Status = PersistedMessageStatus.Processed;
        ProcessedAtUtc = DateTimeOffset.UtcNow;
        LockedAtUtc = null;
        LockedBy = null;
        Error = null;
    }

    public void MarkFailed(string error, Func<int, TimeSpan> retryDelayProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        ArgumentNullException.ThrowIfNull(retryDelayProvider);

        AttemptCount += 1;
        Error = error.Trim();
        FailedAtUtc = DateTimeOffset.UtcNow;
        LockedAtUtc = null;
        LockedBy = null;

        if (AttemptCount >= MaxAttempts)
        {
            Status = PersistedMessageStatus.DeadLettered;
            NextAttemptAtUtc = DateTimeOffset.MaxValue;
            return;
        }

        Status = PersistedMessageStatus.Failed;
        NextAttemptAtUtc = DateTimeOffset.UtcNow.Add(retryDelayProvider(AttemptCount));
    }

    public void Requeue()
    {
        if (Status != PersistedMessageStatus.Failed)
        {
            return;
        }

        Status = PersistedMessageStatus.Pending;
    }
}
