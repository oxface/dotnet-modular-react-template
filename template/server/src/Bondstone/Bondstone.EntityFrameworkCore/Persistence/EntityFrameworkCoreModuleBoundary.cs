using Bondstone.Internal;

namespace Bondstone.EntityFrameworkCore.Persistence;

public sealed class EntityFrameworkCoreModuleBoundary(
    IModulePersistenceResolver persistenceResolver)
    : IModuleBoundary
{
    public async ValueTask ExecuteAsync(
        string moduleName,
        Func<CancellationToken, ValueTask> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await ExecuteAsync(
            moduleName,
            async ct =>
            {
                await operation(ct);
                return true;
            },
            cancellationToken);
    }

    public ValueTask<T> ExecuteAsync<T>(
        string moduleName,
        Func<CancellationToken, ValueTask<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        string normalizedModuleName = moduleName.TrimRequired(nameof(moduleName));
        IModuleUnitOfWork unitOfWork = persistenceResolver.ResolveUnitOfWork(normalizedModuleName);

        return unitOfWork.ExecuteTransactionalAsync(operation, cancellationToken);
    }
}
