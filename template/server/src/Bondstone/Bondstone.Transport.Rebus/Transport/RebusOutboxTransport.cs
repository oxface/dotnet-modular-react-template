using System.Globalization;
using System.Text.Json;
using Bondstone.Messaging;
using Rebus.Bus;
using Rebus.Messages;
using Rebus.ServiceProvider;

namespace Bondstone.Transport.Rebus;

public sealed class RebusOutboxTransport(
    IBusRegistry busRegistry,
    IMessageTypeRegistry messageTypeRegistry,
    IOutboxRouteResolver routeResolver) : IOutboxTransport
{
    public async Task DispatchAsync(IDurableOutboxMessage outboxMessage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outboxMessage);

        Type messageClrType = messageTypeRegistry.ResolveClrType(outboxMessage.MessageType);
        object message = JsonSerializer.Deserialize(outboxMessage.Payload, messageClrType)
            ?? throw new InvalidOperationException(
                $"Outbox message '{outboxMessage.MessageId}' payload could not be deserialized as '{messageClrType.FullName}'.");

        OutboxRoute route = routeResolver.Resolve(outboxMessage);
        IBus bus = busRegistry.GetBus(route.BusKey);
        Dictionary<string, string> headers = CreateHeaders(outboxMessage);

        if (outboxMessage.MessageKind == MessageKind.Event)
        {
            await bus.Publish(message, headers);
            return;
        }

        if (string.IsNullOrWhiteSpace(route.DestinationAddress))
        {
            throw new InvalidOperationException(
                $"Outbox command message '{outboxMessage.MessageId}' (type '{outboxMessage.MessageType}') requires a destination address.");
        }

        await bus.Advanced.Routing.Send(route.DestinationAddress, message, headers);
    }

    private static Dictionary<string, string> CreateHeaders(IDurableOutboxMessage message)
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Headers.MessageId] = message.MessageId.ToString("D"),
            [Headers.CorrelationId] = message.CorrelationId.ToString("D"),
            [BondstoneMessageHeaders.MessageId] = message.MessageId.ToString("D"),
            [BondstoneMessageHeaders.MessageType] = message.MessageType,
            [BondstoneMessageHeaders.SourceModule] = message.SourceModule,
            [BondstoneMessageHeaders.CorrelationId] = message.CorrelationId.ToString("D"),
            [BondstoneMessageHeaders.CreatedAtUtc] = message.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)
        };

        AddIfPresent(headers, BondstoneMessageHeaders.TargetModule, message.TargetModule);
        AddIfPresent(headers, BondstoneMessageHeaders.CausationId, message.CausationId?.ToString("D"));
        AddIfPresent(headers, BondstoneMessageHeaders.DurableOperationId, message.DurableOperationId?.ToString("D"));
        AddIfPresent(headers, BondstoneMessageHeaders.PartitionKey, message.PartitionKey);
        RebusMessageDiagnostics.AddTraceHeaders(headers, message.Metadata);

        return headers;
    }

    private static void AddIfPresent(Dictionary<string, string> headers, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            headers[key] = value;
        }
    }
}
