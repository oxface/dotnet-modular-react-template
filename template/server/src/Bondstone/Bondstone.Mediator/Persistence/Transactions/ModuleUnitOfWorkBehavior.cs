using Mediator;
using Bondstone;

namespace Bondstone.Mediator.Persistence.Transactions;

public sealed class ModuleUnitOfWorkBehavior<TCommand, TResponse>(
    IModuleCommandBoundaryResolver commandBoundaryResolver,
    IModuleBoundary moduleBoundary)
    : IPipelineBehavior<TCommand, TResponse>
    where TCommand : IBaseCommand
{
    public async ValueTask<TResponse> Handle(
        TCommand message,
        MessageHandlerDelegate<TCommand, TResponse> next,
        CancellationToken cancellationToken)
    {
        string? moduleName = commandBoundaryResolver.ResolveModuleName(typeof(TCommand));

        if (moduleName is null)
        {
            if (IsNonPersistentCommand())
            {
                return await next(message, cancellationToken);
            }

            throw new InvalidOperationException(
                $"Command type '{typeof(TCommand).FullName}' is not mapped to a module persistence registration. " +
                $"Register its handler assembly with module persistence or mark the command with {nameof(NonPersistentCommandAttribute)}.");
        }

        return await moduleBoundary.ExecuteAsync(
            moduleName,
            ct => next(message, ct),
            cancellationToken);
    }

    private static bool IsNonPersistentCommand()
    {
        return typeof(TCommand)
            .GetCustomAttributes(typeof(NonPersistentCommandAttribute), inherit: false)
            .Any();
    }
}
