using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Bondstone.Commands;

internal sealed class ModuleCommandBus(IServiceProvider serviceProvider) : IModuleCommandBus
{
    private static readonly MethodInfo SendTypedMethod = typeof(ModuleCommandBus)
        .GetMethod(nameof(SendTypedAsync), BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            $"{nameof(ModuleCommandBus)}.{nameof(SendTypedAsync)} could not be found.");

    public async ValueTask<TResult> SendAsync<TResult>(
        IModuleCommand<TResult> command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        MethodInfo sendTyped = SendTypedMethod.MakeGenericMethod(command.GetType(), typeof(TResult));
        var result = (ValueTask<TResult>)sendTyped.Invoke(this, [command, cancellationToken])!;
        return await result;
    }

    private async ValueTask<TResult> SendTypedAsync<TCommand, TResult>(
        TCommand command,
        CancellationToken cancellationToken)
        where TCommand : IModuleCommand<TResult>
    {
        IModuleCommandHandler<TCommand, TResult> handler =
            serviceProvider.GetRequiredService<IModuleCommandHandler<TCommand, TResult>>();

        ModuleCommandHandlerDelegate<TCommand, TResult> next = handler.HandleAsync;
        IModuleCommandPipelineBehavior<TCommand, TResult>[] behaviors = serviceProvider
            .GetServices<IModuleCommandPipelineBehavior<TCommand, TResult>>()
            .Reverse()
            .ToArray();

        foreach (IModuleCommandPipelineBehavior<TCommand, TResult> behavior in behaviors)
        {
            ModuleCommandHandlerDelegate<TCommand, TResult> inner = next;
            next = (message, ct) => behavior.HandleAsync(message, inner, ct);
        }

        return await next(command, cancellationToken);
    }
}
