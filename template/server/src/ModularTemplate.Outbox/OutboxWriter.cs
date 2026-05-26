using Microsoft.EntityFrameworkCore;

namespace ModularTemplate.Outbox;

public sealed class OutboxWriter<TDbContext>(TDbContext context) : IOutboxWriter
    where TDbContext : DbContext, IModuleDbContext
{
    public void Write(OutboxMessage outboxMessage) => context.OutboxMessages.Add(outboxMessage);
}
