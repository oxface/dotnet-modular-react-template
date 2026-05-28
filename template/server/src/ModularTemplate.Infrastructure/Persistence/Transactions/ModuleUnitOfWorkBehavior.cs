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
        TResponse response = await next(message, cancellationToken);

        if (unitOfWork is not null)
        {
            await unitOfWork.SaveChangesTransactionalAsync(cancellationToken);
        }

        return response;
    }
}
