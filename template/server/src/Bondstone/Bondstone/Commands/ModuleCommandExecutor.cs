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

        IModuleCommandHandler<TCommand, TResult>[] handlers = serviceProvider
            .GetServices<IModuleCommandHandler<TCommand, TResult>>()
            .ToArray();

        IModuleCommandHandler<TCommand, TResult> handler = handlers.Length switch
        {
            1 => handlers[0],
            0 => throw new InvalidOperationException(
                $"No module command handler is registered for command '{typeof(TCommand).FullName}'."),
            _ => throw new InvalidOperationException(
                $"Multiple module command handlers are registered for command '{typeof(TCommand).FullName}'. " +
                "Use exactly one command handler per command type and fan out inside that handler when needed.")
        };

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
