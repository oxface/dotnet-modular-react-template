namespace Bondstone.EntityFrameworkCore.Persistence;

public interface IModuleUnitOfWorkResolver
{
    IModuleUnitOfWork? Resolve(Type commandType);
}
