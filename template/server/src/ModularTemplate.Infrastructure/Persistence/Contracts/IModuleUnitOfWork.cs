namespace ModularTemplate.Infrastructure.Persistence;

public interface IModuleUnitOfWork
{
    string ModuleName { get; }

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task SaveChangesTransactionalAsync(CancellationToken cancellationToken = default);
}
