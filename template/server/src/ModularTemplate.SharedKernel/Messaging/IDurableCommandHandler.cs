namespace ModularTemplate.SharedKernel.Messaging;

/// <summary>
/// Handles a durable command after it is delivered to the target module inbox.
/// Handlers complete by updating module state, emitting events, or updating an
/// operation/read model; they do not return a response payload to the sender.
/// </summary>
public interface IDurableCommandHandler<in TCommand>
    where TCommand : IDurableCommand
{
    Task HandleAsync(TCommand command, MessageContext context, CancellationToken cancellationToken);
}
