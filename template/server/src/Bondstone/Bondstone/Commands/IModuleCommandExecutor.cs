namespace Bondstone.Commands;

public interface IModuleCommandExecutor<TCommand, TResult>
    where TCommand : IModuleCommand<TResult>
{
    ValueTask<TResult> SendAsync(
        TCommand command,
        CancellationToken cancellationToken = default);
}
