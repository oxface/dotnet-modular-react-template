namespace Bondstone;

public interface IModuleCommandBoundaryResolver
{
    string? ResolveModuleName(Type commandType);
}
