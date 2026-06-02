namespace Bondstone.Commands;

public interface IModuleCommandBus
{
    ValueTask<TResult> SendAsync<TResult>(
        IModuleCommand<TResult> command,
        CancellationToken cancellationToken = default);
}
