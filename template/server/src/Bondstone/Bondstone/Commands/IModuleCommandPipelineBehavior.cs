namespace Bondstone.Commands;

public delegate ValueTask<TResult> ModuleCommandHandlerDelegate<TCommand, TResult>(
    TCommand command,
    CancellationToken cancellationToken)
    where TCommand : IModuleCommand<TResult>;

public interface IModuleCommandPipelineBehavior<TCommand, TResult>
    where TCommand : IModuleCommand<TResult>
{
    ValueTask<TResult> HandleAsync(
        TCommand command,
        ModuleCommandHandlerDelegate<TCommand, TResult> next,
        CancellationToken cancellationToken);
}
