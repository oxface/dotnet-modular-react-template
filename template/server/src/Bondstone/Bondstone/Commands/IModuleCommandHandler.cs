namespace Bondstone.Commands;

public interface IModuleCommandHandler<in TCommand, TResult>
    where TCommand : IModuleCommand<TResult>
{
    ValueTask<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
