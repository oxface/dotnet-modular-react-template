using Microsoft.EntityFrameworkCore;
using ModularTemplate.Identity.Access;
using ModularTemplate.Identity.Users;
using Bondstone.EntityFrameworkCore.Persistence;
using Bondstone.EntityFrameworkCore.Postgres.Persistence;

namespace ModularTemplate.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : ModuleDbContext<IdentityDbContext>(options)
{
    public DbSet<LocalUser> LocalUsers => Set<LocalUser>();

    public DbSet<ApplicationAccess> ApplicationAccess => Set<ApplicationAccess>();

    public override string ModuleName => "identity";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        modelBuilder.ApplyPostgresModuleMessagingPersistence(ModuleName);
    }
}
