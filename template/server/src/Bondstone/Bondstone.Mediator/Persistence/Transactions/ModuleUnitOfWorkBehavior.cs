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
            return await next(message, cancellationToken);
        }

        return await moduleBoundary.ExecuteAsync(
            moduleName,
            ct => next(message, ct),
            cancellationToken);
    }
}
