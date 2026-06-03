using Microsoft.EntityFrameworkCore;
using ModularTemplate.Products.Products;
using Bondstone.EntityFrameworkCore.Persistence;

namespace ModularTemplate.Products.Infrastructure.Persistence;

public sealed class ProductsDbContext(DbContextOptions<ProductsDbContext> options)
    : ModuleDbContext<ProductsDbContext>(options)
{
    public DbSet<Product> Products => Set<Product>();

    public override string ModuleName => "products";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("products");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProductsDbContext).Assembly);
        ApplyModuleMessagingPersistence(modelBuilder);
    }
}
