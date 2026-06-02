using Bondstone.Messaging;

namespace Bondstone.Transport.Rebus;

public interface IOutboxRouteResolver
{
    OutboxRoute Resolve(IDurableOutboxMessage message);
}
