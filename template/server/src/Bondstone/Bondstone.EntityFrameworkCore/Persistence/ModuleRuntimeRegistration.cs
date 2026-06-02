namespace Bondstone.EntityFrameworkCore.Persistence;

public sealed class ModuleRuntimeRegistration(
    string moduleName,
    Type dbContextType,
    Type boundaryExecutorType,
    Type inboxExecutorType,
    Type outboxDispatcherType,
    Type outboxWorkerType)
{
    public string ModuleName { get; } = moduleName;

    public Type DbContextType { get; } = dbContextType;

    public Type BoundaryExecutorType { get; } = boundaryExecutorType;

    public Type InboxExecutorType { get; } = inboxExecutorType;

    public Type OutboxDispatcherType { get; } = outboxDispatcherType;

    public Type OutboxWorkerType { get; } = outboxWorkerType;
}
