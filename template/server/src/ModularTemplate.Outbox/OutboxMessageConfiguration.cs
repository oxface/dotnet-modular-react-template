using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModularTemplate.Outbox;

public sealed class OutboxMessageConfiguration(string schema)
    : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages", schema);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MessageId).IsRequired();
        builder.Property(x => x.MessageKind).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.MessageType).HasMaxLength(256).IsRequired();
        builder.Property(x => x.SourceModule).HasMaxLength(128).IsRequired();
        builder.Property(x => x.TargetModule).HasMaxLength(128);
        builder.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Metadata).HasColumnType("jsonb");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Error).HasMaxLength(2048);
        builder.Property(x => x.LockedBy).HasMaxLength(128);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.NextAttemptAtUtc).IsRequired();
        builder.HasIndex(x => x.MessageId).IsUnique();
        builder.HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
        builder.HasIndex(x => x.MessageType);
    }
}
