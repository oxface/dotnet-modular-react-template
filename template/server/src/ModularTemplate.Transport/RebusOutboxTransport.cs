using ModularTemplate.Outbox;
using Rebus.Bus;

namespace ModularTemplate.Transport;

public sealed class RebusOutboxTransport(IBus bus) : IOutboxTransport
{
    public Task DispatchAsync(OutboxMessage outboxMessage, string targetModule, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outboxMessage);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetModule);

        var envelope = new DurableTransportEnvelope(
            outboxMessage.MessageId,
            outboxMessage.MessageKind,
            outboxMessage.MessageType,
            outboxMessage.SourceModule,
            targetModule,
            outboxMessage.CorrelationId,
            outboxMessage.CausationId,
            outboxMessage.OperationId,
            outboxMessage.Payload,
            outboxMessage.Metadata,
            outboxMessage.CreatedAtUtc,
            outboxMessage.MaxAttempts);

        return bus.SendLocal(envelope);
    }
}
