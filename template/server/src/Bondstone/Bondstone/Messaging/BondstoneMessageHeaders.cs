namespace Bondstone.Messaging;

public static class BondstoneMessageHeaders
{
    public const string MessageId = "bondstone-message-id";
    public const string MessageType = "bondstone-message-type";
    public const string SourceModule = "bondstone-source-module";
    public const string TargetModule = "bondstone-target-module";
    public const string CorrelationId = "bondstone-correlation-id";
    public const string CausationId = "bondstone-causation-id";
    public const string DurableOperationId = "bondstone-durable-operation-id";
    public const string CreatedAtUtc = "bondstone-created-at-utc";
}
