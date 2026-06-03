using Microsoft.Extensions.DependencyInjection;

namespace Bondstone.Commands;

internal sealed class ModuleCommandExecutor<TCommand, TResult>(
    IServiceProvider serviceProvider)
    : IModuleCommandExecutor<TCommand, TResult>
    where TCommand : IModuleCommand<TResult>
{
    public async ValueTask<TResult> SendAsync(
        TCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        IModuleCommandHandler<TCommand, TResult> handler =
            serviceProvider.GetRequiredService<IModuleCommandHandler<TCommand, TResult>>();

        ModuleCommandHandlerDelegate<TCommand, TResult> next = handler.HandleAsync;
        IModuleCommandPipelineBehavior<TCommand, TResult>[] behaviors = serviceProvider
            .GetServices<IModuleCommandPipelineBehavior<TCommand, TResult>>()
            .ToArray();

        foreach (IModuleCommandPipelineBehavior<TCommand, TResult> behavior in behaviors)
        {
            ModuleCommandHandlerDelegate<TCommand, TResult> inner = next;
            next = (message, ct) => behavior.HandleAsync(message, inner, ct);
        }

        return await next(command, cancellationToken);
    }
}
