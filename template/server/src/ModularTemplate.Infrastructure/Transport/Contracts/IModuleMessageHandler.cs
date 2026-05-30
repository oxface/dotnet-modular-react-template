namespace ModularTemplate.Infrastructure.Transport;

public interface IModuleMessageHandler<in TMessage>
{
    Task HandleAsync(TMessage message, CancellationToken cancellationToken);
}
