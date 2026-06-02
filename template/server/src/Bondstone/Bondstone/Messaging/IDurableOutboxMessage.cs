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

    Guid? OperationId { get; }

    string Payload { get; }

    DateTimeOffset CreatedAtUtc { get; }
}
