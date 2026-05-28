namespace ModularTemplate.SharedKernel.Messaging;

/// <summary>
/// Marker for asynchronous commands persisted through the outbox/inbox pipeline.
/// Durable commands are send-and-forget: callers observe acceptance and optional
/// operation status, not a direct command result.
/// </summary>
public interface IDurableCommand : IMessage
{
}
