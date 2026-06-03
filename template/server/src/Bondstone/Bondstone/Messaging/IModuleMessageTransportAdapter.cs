using Microsoft.Extensions.DependencyInjection;

namespace Bondstone.Messaging;

public interface IModuleMessageTransportAdapter
{
    void RegisterHandlerAdapter(IServiceCollection services, Type messageType);
}
