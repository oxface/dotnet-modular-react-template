using Bondstone.Messaging;

namespace Bondstone.EntityFrameworkCore.Outbox;

public sealed class OutboxMessage : IDurableOutboxMessage
{
    public const int MaxErrorLength = 2048;
    public const int MaxPartitionKeyLength = 512;

    private OutboxMessage(
        Guid id,
        Guid messageId,
        MessageKind messageKind,
        string messageType,
        string sourceModule,
        string? targetModule,
        Guid correlationId,
        Guid? causationId,
        Guid? durableOperationId,
        string? partitionKey,
        string payload,
        string? metadata,
        DateTimeOffset createdAtUtc,
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
        DurableOperationId = durableOperationId;
        PartitionKey = partitionKey;
        Payload = payload;
        Metadata = metadata;
        Status = PersistedMessageStatus.Pending;
        AttemptCount = 0;
        MaxAttempts = maxAttempts;
        NextAttemptAtUtc = createdAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    public Guid MessageId { get; private set; }

    public MessageKind MessageKind { get; private set; }

    public string MessageType { get; private set; } = string.Empty;

    public string SourceModule { get; private set; } = string.Empty;

    public string? TargetModule { get; private set; }

    public Guid CorrelationId { get; private set; }

    public Guid? CausationId { get; private set; }

    public Guid? DurableOperationId { get; private set; }

    public string? PartitionKey { get; private set; }

    public string Payload { get; private set; } = string.Empty;

    public string? Metadata { get; private set; }

    public PersistedMessageStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public int MaxAttempts { get; private set; }

    public DateTimeOffset NextAttemptAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? DispatchedAtUtc { get; private set; }

    public DateTimeOffset? LockedAtUtc { get; private set; }

    public string? LockedBy { get; private set; }

    public DateTimeOffset? FailedAtUtc { get; private set; }

    public string? Error { get; private set; }

    public static OutboxMessage Create(
        Guid messageId,
        MessageKind messageKind,
        string messageType,
        string sourceModule,
        string? targetModule,
        Guid correlationId,
        Guid? causationId,
        Guid? durableOperationId,
        string payload,
        string? partitionKey = null,
        string? metadata = null,
        int maxAttempts = 5)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceModule);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "Max attempts must be greater than zero.");
        }

        string? normalizedTargetModule = targetModule?.Trim();
        if (messageKind == MessageKind.Command && string.IsNullOrWhiteSpace(normalizedTargetModule))
        {
            throw new ArgumentException("Command outbox messages require a target module.", nameof(targetModule));
        }

        if (messageKind == MessageKind.Event && !string.IsNullOrWhiteSpace(normalizedTargetModule))
        {
            throw new ArgumentException("Event outbox messages must not specify a target module.", nameof(targetModule));
        }

        if (messageKind is not MessageKind.Command and not MessageKind.Event)
        {
            throw new ArgumentOutOfRangeException(nameof(messageKind), messageKind, "Unsupported outbox message kind.");
        }

        normalizedTargetModule = string.IsNullOrWhiteSpace(normalizedTargetModule)
            ? null
            : normalizedTargetModule;
        string? normalizedPartitionKey = NormalizePartitionKey(partitionKey);

        return new OutboxMessage(
            Guid.NewGuid(),
            messageId,
            messageKind,
            messageType.Trim(),
            sourceModule.Trim(),
            normalizedTargetModule,
            correlationId,
            causationId,
            durableOperationId,
            normalizedPartitionKey,
            payload,
            metadata,
            DateTimeOffset.UtcNow,
            maxAttempts);
    }

    public void MarkProcessed()
    {
        Status = PersistedMessageStatus.Processed;
        DispatchedAtUtc = DateTimeOffset.UtcNow;
        LockedAtUtc = null;
        LockedBy = null;
        Error = null;
    }

    public void RefreshLock()
    {
        if (Status == PersistedMessageStatus.Processing)
        {
            LockedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    public void MarkFailed(string error, Func<int, TimeSpan> retryDelayProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        ArgumentNullException.ThrowIfNull(retryDelayProvider);

        AttemptCount += 1;
        Error = TruncateError(error.Trim());
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

    public void RequeueDeadLettered()
    {
        if (Status != PersistedMessageStatus.DeadLettered)
        {
            throw new InvalidOperationException(
                $"Only {PersistedMessageStatus.DeadLettered} outbox messages can be requeued.");
        }

        Status = PersistedMessageStatus.Pending;
        AttemptCount = 0;
        NextAttemptAtUtc = DateTimeOffset.UtcNow;
        LockedAtUtc = null;
        LockedBy = null;
        FailedAtUtc = null;
        Error = null;
    }

    private static string TruncateError(string error)
    {
        if (error.Length <= MaxErrorLength)
        {
            return error;
        }

        return error[..MaxErrorLength];
    }

    private static string? NormalizePartitionKey(string? partitionKey)
    {
        if (string.IsNullOrWhiteSpace(partitionKey))
        {
            return null;
        }

        string normalizedPartitionKey = partitionKey.Trim();
        if (normalizedPartitionKey.Length > MaxPartitionKeyLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(partitionKey),
                partitionKey,
                $"Partition key must be at most {MaxPartitionKeyLength} characters.");
        }

        return normalizedPartitionKey;
    }
}
