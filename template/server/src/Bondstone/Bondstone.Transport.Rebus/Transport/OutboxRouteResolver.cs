using Microsoft.Extensions.Options;
using Bondstone.Internal;
using Bondstone.Messaging;

namespace Bondstone.Transport.Rebus;

public sealed class OutboxRouteResolver(
    IOptions<DurableMessagingOptions> messagingOptions,
    IOptions<RebusTransportOptions> transportOptions) : IOutboxRouteResolver
{
    private readonly DurableMessagingOptions _messagingOptions = messagingOptions.Value;
    private readonly RebusTransportOptions _transportOptions = transportOptions.Value;

    public OutboxRoute Resolve(IDurableOutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        string sourceModule = NormalizeModule(message.SourceModule, nameof(message.SourceModule));
        ValidateConfiguredModule(sourceModule, nameof(message.SourceModule));
        string busKey = MessagingBusKeys.ModuleQueue(sourceModule);

        if (message.MessageKind == MessageKind.Event)
        {
            return new OutboxRoute(busKey, DestinationAddress: null);
        }

        string targetModule = NormalizeModule(message.TargetModule, nameof(message.TargetModule));
        ValidateConfiguredModule(targetModule, nameof(message.TargetModule));
        return new OutboxRoute(busKey, BuildModuleQueueName(targetModule));
    }

    private string BuildModuleQueueName(string moduleName)
    {
        return $"{_transportOptions.QueuePrefix}.{moduleName}";
    }

    private static string NormalizeModule(string? moduleName, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            throw new InvalidOperationException($"{parameterName} is required for outbox routing.");
        }

        return moduleName.TrimRequired(parameterName);
    }

    private void ValidateConfiguredModule(string moduleName, string parameterName)
    {
        if (_messagingOptions.Modules.ContainsTrimmedOrdinal(moduleName))
        {
            return;
        }

        throw new InvalidOperationException(
            $"{parameterName} '{moduleName}' is not listed in Messaging:Modules.");
    }
}
