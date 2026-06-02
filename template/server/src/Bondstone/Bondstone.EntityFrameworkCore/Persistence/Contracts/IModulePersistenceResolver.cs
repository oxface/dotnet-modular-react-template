using Bondstone.EntityFrameworkCore.Outbox;

namespace Bondstone.EntityFrameworkCore.Persistence;

public interface IModulePersistenceResolver
{
    IModuleDbContext ResolveDbContext(string moduleName);

    IModuleUnitOfWork ResolveUnitOfWork(string moduleName);

    IOutboxWriter ResolveOutboxWriter(string moduleName);
}
