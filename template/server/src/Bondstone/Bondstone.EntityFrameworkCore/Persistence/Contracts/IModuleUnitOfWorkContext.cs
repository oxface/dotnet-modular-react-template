namespace Bondstone.EntityFrameworkCore.Persistence;

public interface IModuleUnitOfWorkContext
{
    string? CurrentModuleName { get; }

    IDisposable StartModuleScope(string moduleName);
}
