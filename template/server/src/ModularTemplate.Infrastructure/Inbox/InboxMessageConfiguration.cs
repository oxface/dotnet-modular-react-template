using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModularTemplate.Infrastructure.Inbox;

public sealed class InboxMessageConfiguration(string schema)
    : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages", schema);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MessageId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ModuleName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.HandlerName).HasMaxLength(512).IsRequired();
        builder.Property(x => x.ReceivedAtUtc).IsRequired();
        builder.Property(x => x.ProcessedAtUtc);
        builder.HasIndex(x => new { x.ModuleName, x.MessageId, x.HandlerName }).IsUnique();
        builder.HasIndex(x => x.ReceivedAtUtc);
    }
}
