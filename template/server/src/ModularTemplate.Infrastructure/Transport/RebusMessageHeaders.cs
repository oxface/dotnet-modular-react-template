namespace ModularTemplate.Infrastructure.Transport;

internal static class RebusMessageHeaders
{
    public const string MessageId = "modular-template-message-id";
    public const string MessageType = "modular-template-message-type";
    public const string SourceModule = "modular-template-source-module";
    public const string TargetModule = "modular-template-target-module";
    public const string ReceivingModule = "modular-template-receiving-module";
    public const string CorrelationId = "modular-template-correlation-id";
    public const string CausationId = "modular-template-causation-id";
    public const string OperationId = "modular-template-operation-id";
    public const string CreatedAtUtc = "modular-template-created-at-utc";
}
