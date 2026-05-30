using Mediator;
using ModularTemplate.Infrastructure.Outbox;
using ModularTemplate.SharedKernel.Extensions;

namespace ModularTemplate.Infrastructure.Persistence;

public sealed class ModulePersistenceRegistration
{
    public ModulePersistenceRegistration(
        string moduleName,
        Type dbContextType,
        IReadOnlyCollection<Type> commandTypes,
        Func<IServiceProvider, IModuleDbContext>? dbContextFactory = null,
        Func<IServiceProvider, IModuleUnitOfWork>? unitOfWorkFactory = null,
        Func<IServiceProvider, IOutboxWriter>? outboxWriterFactory = null)
    {
        ArgumentNullException.ThrowIfNull(dbContextType);
        ArgumentNullException.ThrowIfNull(commandTypes);

        Type[] normalizedCommandTypes = commandTypes
            .Select(commandType => commandType
                ?? throw new ArgumentException(
                    "Command types must not contain null values.",
                    nameof(commandTypes)))
            .Distinct()
            .ToArray();

        foreach (Type commandType in normalizedCommandTypes)
        {
            if (!typeof(IBaseCommand).IsAssignableFrom(commandType))
            {
                throw new ArgumentException(
                    $"Type '{commandType.FullName}' must implement {nameof(IBaseCommand)}.",
                    nameof(commandTypes));
            }
        }

        ModuleName = moduleName.TrimRequired(nameof(moduleName));
        DbContextType = dbContextType;
        CommandTypes = normalizedCommandTypes;
        DbContextFactory = dbContextFactory ?? MissingDbContextFactory;
        UnitOfWorkFactory = unitOfWorkFactory ?? MissingUnitOfWorkFactory;
        OutboxWriterFactory = outboxWriterFactory ?? MissingOutboxWriterFactory;
    }

    public string ModuleName { get; }

    public Type DbContextType { get; }

    public IReadOnlyCollection<Type> CommandTypes { get; }

    internal Func<IServiceProvider, IModuleDbContext> DbContextFactory { get; }

    internal Func<IServiceProvider, IModuleUnitOfWork> UnitOfWorkFactory { get; }

    internal Func<IServiceProvider, IOutboxWriter> OutboxWriterFactory { get; }

    public bool HandlesCommand(Type commandType)
    {
        ArgumentNullException.ThrowIfNull(commandType);

        return CommandTypes.Contains(commandType);
    }

    private static IModuleDbContext MissingDbContextFactory(IServiceProvider serviceProvider)
    {
        throw new InvalidOperationException("Module DbContext factory is not configured for this registration.");
    }

    private static IModuleUnitOfWork MissingUnitOfWorkFactory(IServiceProvider serviceProvider)
    {
        throw new InvalidOperationException("Module unit of work factory is not configured for this registration.");
    }

    private static IOutboxWriter MissingOutboxWriterFactory(IServiceProvider serviceProvider)
    {
        throw new InvalidOperationException("Module outbox writer factory is not configured for this registration.");
    }
}
