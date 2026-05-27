using Mediator;

namespace ModularTemplate.Infrastructure.Persistence.Transactions;

public sealed class ModuleUnitOfWorkBehavior<TCommand, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TCommand, TResponse>
    where TCommand : IBaseCommand
{
    public async ValueTask<TResponse> Handle(
        TCommand message,
        MessageHandlerDelegate<TCommand, TResponse> next,
        CancellationToken cancellationToken)
    {
        TResponse response = await next(message, cancellationToken);
        await unitOfWork.SaveChangesTransactionalAsync(cancellationToken);
        return response;
    }
}
