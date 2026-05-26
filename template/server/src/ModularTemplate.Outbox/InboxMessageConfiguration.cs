using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModularTemplate.Outbox;

public sealed class InboxMessageConfiguration(string schema)
    : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages", schema);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MessageId).IsRequired();
        builder.Property(x => x.MessageKind).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.MessageType).HasMaxLength(256).IsRequired();
        builder.Property(x => x.SourceModule).HasMaxLength(128).IsRequired();
        builder.Property(x => x.TargetModule).HasMaxLength(128).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128);
        builder.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Metadata).HasColumnType("jsonb");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Error).HasMaxLength(2048);
        builder.Property(x => x.LockedBy).HasMaxLength(128);
        builder.Property(x => x.ReceivedAtUtc).IsRequired();
        builder.Property(x => x.NextAttemptAtUtc).IsRequired();
        builder.HasIndex(x => new { x.MessageId, x.TargetModule }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
        builder.HasIndex(x => x.MessageType);
    }
}
