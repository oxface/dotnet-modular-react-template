namespace Bondstone.EntityFrameworkCore.Persistence;

public interface IEntityFrameworkCoreModuleMigrator
{
    Task MigrateAsync(string? moduleName = null, CancellationToken cancellationToken = default);
}
