using Bondstone.Commands;

namespace Bondstone.EntityFrameworkCore.Persistence;

public sealed class ModuleUnitOfWorkCommandBehavior<TCommand, TResult>(
    IModuleCommandBoundaryResolver commandBoundaryResolver,
    IModuleBoundary moduleBoundary)
    : IModuleCommandPipelineBehavior<TCommand, TResult>
    where TCommand : IModuleCommand<TResult>
{
    public async ValueTask<TResult> HandleAsync(
        TCommand command,
        ModuleCommandHandlerDelegate<TCommand, TResult> next,
        CancellationToken cancellationToken)
    {
        string? moduleName = commandBoundaryResolver.ResolveModuleName(typeof(TCommand));

        if (moduleName is null)
        {
            if (IsNonPersistentCommand())
            {
                return await next(command, cancellationToken);
            }

            throw new InvalidOperationException(
                $"Command type '{typeof(TCommand).FullName}' is not mapped to a module persistence registration. " +
                $"Register its handler assembly with module persistence or mark the command with {nameof(NonPersistentCommandAttribute)}.");
        }

        return await moduleBoundary.ExecuteAsync(
            moduleName,
            ct => next(command, ct),
            cancellationToken);
    }

    private static bool IsNonPersistentCommand()
    {
        return typeof(TCommand)
            .GetCustomAttributes(typeof(NonPersistentCommandAttribute), inherit: false)
            .Any();
    }
}
