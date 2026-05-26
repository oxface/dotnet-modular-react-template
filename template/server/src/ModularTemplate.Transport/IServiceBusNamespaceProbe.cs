namespace ModularTemplate.Transport;

public interface IServiceBusNamespaceProbe
{
    Task ProbeAsync(string connectionString, CancellationToken cancellationToken);
}
