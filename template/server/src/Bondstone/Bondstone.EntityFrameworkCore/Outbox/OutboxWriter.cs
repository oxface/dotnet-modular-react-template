using Microsoft.EntityFrameworkCore;
using Bondstone.EntityFrameworkCore.Persistence;

namespace Bondstone.EntityFrameworkCore.Outbox;

public sealed class OutboxWriter<TDbContext>(TDbContext context) : IOutboxWriter
    where TDbContext : DbContext, IModuleDbContext
{
    public string ModuleName => context.ModuleName;

    public void Write(OutboxMessage outboxMessage) => context.OutboxMessages.Add(outboxMessage);
}
