namespace ModularTemplate.Infrastructure.Persistence;

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task SaveChangesTransactionalAsync(CancellationToken cancellationToken = default);
}
