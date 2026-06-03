namespace Bondstone.Messaging;

public interface IDurableOutboxMessage
{
    Guid MessageId { get; }

    MessageKind MessageKind { get; }

    string MessageType { get; }

    string SourceModule { get; }

    string? TargetModule { get; }

    Guid CorrelationId { get; }

    Guid? CausationId { get; }

    Guid? DurableOperationId { get; }

    string? PartitionKey { get; }

    string Payload { get; }

    string? Metadata { get; }

    DateTimeOffset CreatedAtUtc { get; }
}
