namespace Bondstone.Messaging;

/// <summary>
/// Persists durable commands for asynchronous delivery through the outbox
/// pipeline. Submission acknowledges accepted work; command results are observed
/// later through operation/read-model queries or follow-up events.
/// </summary>
public interface IDurableCommandSender
{
    CommandSubmission Send<TCommand>(
        TCommand command,
        string targetModule,
        Guid? durableOperationId = null,
        Guid? causationId = null,
        int? maxAttempts = null)
        where TCommand : IDurableCommand;
}
