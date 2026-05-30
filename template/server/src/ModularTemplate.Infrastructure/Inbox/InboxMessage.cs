namespace ModularTemplate.Infrastructure.Inbox;

public sealed class InboxMessage
{
    private InboxMessage(
        Guid id,
        string messageId,
        string moduleName,
        string handlerName,
        DateTimeOffset receivedAtUtc)
    {
        Id = id;
        MessageId = messageId;
        ModuleName = moduleName;
        HandlerName = handlerName;
        ReceivedAtUtc = receivedAtUtc;
    }

    private InboxMessage()
    {
    }

    public Guid Id { get; private set; }

    public string MessageId { get; private set; } = string.Empty;

    public string ModuleName { get; private set; } = string.Empty;

    public string HandlerName { get; private set; } = string.Empty;

    public DateTimeOffset ReceivedAtUtc { get; private set; }

    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    public static InboxMessage Start(string messageId, string moduleName, string handlerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerName);

        return new InboxMessage(
            Guid.NewGuid(),
            messageId.Trim(),
            moduleName.Trim(),
            handlerName.Trim(),
            DateTimeOffset.UtcNow);
    }

    public bool IsProcessed => ProcessedAtUtc is not null;

    public void MarkProcessed()
    {
        ProcessedAtUtc = DateTimeOffset.UtcNow;
    }
}
