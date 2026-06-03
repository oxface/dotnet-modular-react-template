using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Bondstone.Messaging;
using Rebus.Handlers;

namespace Bondstone.Transport.Rebus;

internal sealed class RebusModuleMessageTransportAdapter : IModuleMessageTransportAdapter
{
    public void RegisterHandlerAdapter(IServiceCollection services, Type messageType)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped(
            typeof(IHandleMessages<>).MakeGenericType(messageType),
            typeof(ModuleScopedRebusHandler<>).MakeGenericType(messageType)));
    }
}
