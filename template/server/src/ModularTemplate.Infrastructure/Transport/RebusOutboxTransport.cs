using System.Globalization;
using System.Text.Json;
using ModularTemplate.Infrastructure.Outbox;
using ModularTemplate.SharedKernel.Messaging;
using Rebus.Bus;
using Rebus.ServiceProvider;

namespace ModularTemplate.Infrastructure.Transport;

public sealed class RebusOutboxTransport(
    IBusRegistry busRegistry,
    IMessageTypeRegistry messageTypeRegistry,
    IOutboxRouteResolver routeResolver) : IOutboxTransport
{
    public async Task DispatchAsync(OutboxMessage outboxMessage, CancellationToken cancellationToken)
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
                $"Outbox command message '{outboxMessage.Id}' (type '{outboxMessage.MessageType}') requires a destination address.");
        }

        await bus.Advanced.Routing.Send(route.DestinationAddress, message, headers);
    }

    private static Dictionary<string, string> CreateHeaders(OutboxMessage message)
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RebusMessageHeaders.MessageId] = message.MessageId.ToString("D"),
            [RebusMessageHeaders.MessageType] = message.MessageType,
            [RebusMessageHeaders.SourceModule] = message.SourceModule,
            [RebusMessageHeaders.CorrelationId] = message.CorrelationId.ToString("D"),
            [RebusMessageHeaders.CreatedAtUtc] = message.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)
        };

        AddIfPresent(headers, RebusMessageHeaders.TargetModule, message.TargetModule);
        AddIfPresent(headers, RebusMessageHeaders.CausationId, message.CausationId?.ToString("D"));
        AddIfPresent(headers, RebusMessageHeaders.OperationId, message.OperationId?.ToString("D"));

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
