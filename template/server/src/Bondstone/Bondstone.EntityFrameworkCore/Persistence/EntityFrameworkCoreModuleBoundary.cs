using Bondstone.Internal;

namespace Bondstone.EntityFrameworkCore.Persistence;

public sealed class EntityFrameworkCoreModuleBoundary(
    IEnumerable<IModuleBoundaryExecutor> executors)
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
        IModuleBoundaryExecutor executor = executors.SingleOrDefault(executor =>
                string.Equals(executor.ModuleName, normalizedModuleName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"No module boundary executor exists for module '{normalizedModuleName}'.");

        return executor.ExecuteAsync(operation, cancellationToken);
    }
}

public interface IModuleBoundaryExecutor
{
    string ModuleName { get; }

    ValueTask<T> ExecuteAsync<T>(
        Func<CancellationToken, ValueTask<T>> operation,
        CancellationToken cancellationToken = default);
}

public sealed class EntityFrameworkCoreModuleBoundary<TDbContext>(
    ModuleUnitOfWork<TDbContext> unitOfWork)
    : IModuleBoundaryExecutor
    where TDbContext : Microsoft.EntityFrameworkCore.DbContext, IModuleDbContext
{
    public string ModuleName => unitOfWork.ModuleName;

    public ValueTask<T> ExecuteAsync<T>(
        Func<CancellationToken, ValueTask<T>> operation,
        CancellationToken cancellationToken = default)
    {
        return unitOfWork.ExecuteTransactionalAsync(operation, cancellationToken);
    }
}
