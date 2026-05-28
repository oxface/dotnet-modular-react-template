namespace ModularTemplate.Infrastructure.Persistence;

public interface IModuleUnitOfWork
{
    string ModuleName { get; }

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    ValueTask<T> ExecuteTransactionalAsync<T>(
        Func<CancellationToken, ValueTask<T>> operation,
        CancellationToken cancellationToken = default);
}
