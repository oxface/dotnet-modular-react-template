namespace ModularTemplate.SharedKernel.Messaging;

public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, MessageContext context, CancellationToken cancellationToken);
}
