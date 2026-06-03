using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bondstone.EntityFrameworkCore.Outbox;

public sealed class OutboxMessageConfiguration(
    string schema)
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
        builder.Property(x => x.Payload).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Error).HasMaxLength(OutboxMessage.MaxErrorLength);
        builder.Property(x => x.LockedBy).HasMaxLength(128);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.NextAttemptAtUtc).IsRequired();
        builder.HasIndex(x => x.MessageId).IsUnique();
        builder.HasIndex(x => new { x.Status, x.NextAttemptAtUtc, x.CreatedAtUtc });
        builder.HasIndex(x => x.MessageType);
    }
}
