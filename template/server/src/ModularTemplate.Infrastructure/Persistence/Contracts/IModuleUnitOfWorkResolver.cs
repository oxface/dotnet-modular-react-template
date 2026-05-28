namespace ModularTemplate.Infrastructure.Persistence;

public interface IModuleUnitOfWorkResolver
{
    IModuleUnitOfWork? Resolve(Type commandType);
}
