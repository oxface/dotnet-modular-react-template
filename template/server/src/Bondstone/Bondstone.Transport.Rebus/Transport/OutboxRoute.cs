namespace Bondstone.Transport.Rebus;

public sealed record OutboxRoute(string BusKey, string? DestinationAddress);
