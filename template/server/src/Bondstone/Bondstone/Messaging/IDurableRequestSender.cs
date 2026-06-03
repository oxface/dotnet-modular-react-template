namespace Bondstone.Messaging;

public interface IDurableRequestSender
{
    Task<DurableRequestResult<TResult>> SendAndWaitAsync<TCommand, TResult>(
        TCommand command,
        string targetModule,
        Guid? durableOperationId = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
        where TCommand : IDurableCommand;
}
