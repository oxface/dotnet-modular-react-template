using Mediator;

namespace ModularTemplate.Infrastructure.Persistence.Transactions;

public sealed class ModuleUnitOfWorkBehavior<TCommand, TResponse>(
    IModuleUnitOfWorkResolver unitOfWorkResolver)
    : IPipelineBehavior<TCommand, TResponse>
    where TCommand : IBaseCommand
{
    public async ValueTask<TResponse> Handle(
        TCommand message,
        MessageHandlerDelegate<TCommand, TResponse> next,
        CancellationToken cancellationToken)
    {
        IModuleUnitOfWork? unitOfWork = unitOfWorkResolver.Resolve(typeof(TCommand));

        if (unitOfWork is null)
        {
            return await next(message, cancellationToken);
        }

        return await unitOfWork.ExecuteTransactionalAsync(
            ct => next(message, ct),
            cancellationToken);
    }
}
