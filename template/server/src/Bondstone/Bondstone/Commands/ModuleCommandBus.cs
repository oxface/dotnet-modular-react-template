using System.Reflection;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Bondstone.Commands;

internal sealed class ModuleCommandBus(IServiceProvider serviceProvider) : IModuleCommandBus
{
    private static readonly ConcurrentDictionary<CommandInvocationKey, CommandInvoker> Invokers = [];

    private static readonly MethodInfo InvokeTypedMethod = typeof(ModuleCommandBus)
        .GetMethod(nameof(InvokeTypedAsync), BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            $"{nameof(ModuleCommandBus)}.{nameof(InvokeTypedAsync)} could not be found.");

    public async ValueTask<TResult> SendAsync<TResult>(
        IModuleCommand<TResult> command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Type commandType = command.GetType();
        CommandInvoker invoker = Invokers.GetOrAdd(
            new CommandInvocationKey(commandType, typeof(TResult)),
            static key => CreateInvoker(key.CommandType, key.ResultType));

        object? result = await invoker(this, command, cancellationToken);
        return result is null
            ? default!
            : (TResult)result;
    }

    private static CommandInvoker CreateInvoker(Type commandType, Type resultType)
    {
        MethodInfo invokeTyped = InvokeTypedMethod.MakeGenericMethod(commandType, resultType);
        return (CommandInvoker)Delegate.CreateDelegate(typeof(CommandInvoker), invokeTyped);
    }

    private async ValueTask<object?> InvokeTypedAsync<TCommand, TResult>(
        object command,
        CancellationToken cancellationToken)
        where TCommand : IModuleCommand<TResult>
    {
        IModuleCommandExecutor<TCommand, TResult> executor =
            serviceProvider.GetRequiredService<IModuleCommandExecutor<TCommand, TResult>>();
        return await executor.SendAsync((TCommand)command, cancellationToken);
    }

    private sealed record CommandInvocationKey(Type CommandType, Type ResultType);

    private delegate ValueTask<object?> CommandInvoker(
        ModuleCommandBus commandBus,
        object command,
        CancellationToken cancellationToken);
}
