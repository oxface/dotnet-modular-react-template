using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularTemplate.Operations.Operations;
using ModularTemplate.Operations.Contracts.Operations;

namespace ModularTemplate.Operations.Infrastructure.Persistence;

public sealed class OperationEntityTypeConfiguration : IEntityTypeConfiguration<Operation>
{
    public void Configure(EntityTypeBuilder<Operation> builder)
    {
        builder.ToTable("operations", "operations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OperationType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(OperationStatus.Pending)
            .IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
        builder.Property(x => x.CompletedAtUtc);
        builder.Property(x => x.FailedAtUtc);
        builder.Property(x => x.FailureReason).HasMaxLength(1024);
        builder.Property(x => x.ResultJson).HasColumnType("jsonb");
        builder.Property(x => x.MetadataJson).HasColumnType("jsonb");
        builder.Ignore(x => x.DomainEvents);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
