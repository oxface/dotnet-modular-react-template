using Microsoft.EntityFrameworkCore;
using ModularTemplate.Infrastructure.Persistence;

namespace ModularTemplate.Infrastructure.Outbox;

public sealed class OutboxWriter<TDbContext>(TDbContext context) : IOutboxWriter
    where TDbContext : DbContext, IModuleDbContext
{
    public void Write(OutboxMessage outboxMessage) => context.OutboxMessages.Add(outboxMessage);
}
