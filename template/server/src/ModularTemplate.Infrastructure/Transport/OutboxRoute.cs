namespace ModularTemplate.Infrastructure.Transport;

public sealed record OutboxRoute(string BusKey, string? DestinationAddress);
