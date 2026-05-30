using ModularTemplate.Infrastructure.Outbox;

namespace ModularTemplate.Infrastructure.Persistence;

public interface IModulePersistenceResolver
{
    IModuleDbContext ResolveDbContext(string moduleName);

    IModuleUnitOfWork ResolveUnitOfWork(string moduleName);

    IOutboxWriter ResolveOutboxWriter(string moduleName);
}
