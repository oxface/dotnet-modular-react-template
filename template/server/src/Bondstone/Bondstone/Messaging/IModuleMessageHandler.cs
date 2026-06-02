namespace Bondstone.Messaging;

public interface IModuleMessageHandler<in TMessage>
{
    Task HandleAsync(TMessage message, CancellationToken cancellationToken);
}
