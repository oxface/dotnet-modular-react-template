namespace Bondstone;

public interface IModuleBoundary
{
    ValueTask ExecuteAsync(
        string moduleName,
        Func<CancellationToken, ValueTask> operation,
        CancellationToken cancellationToken = default);

    ValueTask<T> ExecuteAsync<T>(
        string moduleName,
        Func<CancellationToken, ValueTask<T>> operation,
        CancellationToken cancellationToken = default);
}
