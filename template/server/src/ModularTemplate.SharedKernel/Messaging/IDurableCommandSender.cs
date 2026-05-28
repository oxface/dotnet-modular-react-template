namespace ModularTemplate.SharedKernel.Messaging;

/// <summary>
/// Persists durable commands for asynchronous delivery through the outbox/Rebus
/// pipeline. Submission acknowledges accepted work; command results are observed
/// later through operation/read-model queries or follow-up events.
/// </summary>
public interface IDurableCommandSender
{
    CommandSubmission Send<TCommand>(
        TCommand command,
        DurableCommandSubmissionOptions options)
        where TCommand : IDurableCommand;
}
