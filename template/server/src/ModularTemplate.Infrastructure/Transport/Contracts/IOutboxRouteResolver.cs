using ModularTemplate.Infrastructure.Outbox;

namespace ModularTemplate.Infrastructure.Transport;

public interface IOutboxRouteResolver
{
    OutboxRoute Resolve(OutboxMessage message);
}
