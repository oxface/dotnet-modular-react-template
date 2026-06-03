using Microsoft.EntityFrameworkCore;
using ModularTemplate.Operations.Operations;
using Bondstone.EntityFrameworkCore.Persistence;

namespace ModularTemplate.Operations.Infrastructure.Persistence;

public sealed class OperationsDbContext(DbContextOptions<OperationsDbContext> options)
    : ModuleDbContext<OperationsDbContext>(options)
{
    public DbSet<Operation> Operations => Set<Operation>();

    public override string ModuleName => "operations";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("operations");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OperationsDbContext).Assembly);
        ApplyModuleMessagingPersistence(modelBuilder);
    }
}
