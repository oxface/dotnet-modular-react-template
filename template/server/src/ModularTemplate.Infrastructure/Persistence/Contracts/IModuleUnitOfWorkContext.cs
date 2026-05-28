namespace ModularTemplate.Infrastructure.Persistence;

public interface IModuleUnitOfWorkContext
{
    string? CurrentModuleName { get; }

    IDisposable StartModuleScope(string moduleName);
}
