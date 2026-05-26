namespace ModularTemplate.SharedKernel.Messaging;

public interface IDurableCommandHandler<in TCommand>
    where TCommand : IDurableCommand
{
    Task HandleAsync(TCommand command, MessageContext context, CancellationToken cancellationToken);
}
