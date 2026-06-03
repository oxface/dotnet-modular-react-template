using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularTemplate.Products.Products;

namespace ModularTemplate.Products.Infrastructure.Persistence;

public sealed class ProductEntityTypeConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", "products");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
        builder.Property(x => x.MetadataJson).HasColumnType("jsonb");
        builder.Ignore(x => x.DomainEvents);
        builder.HasIndex(x => x.Name);
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
