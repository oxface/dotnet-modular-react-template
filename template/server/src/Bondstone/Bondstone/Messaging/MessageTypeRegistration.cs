namespace Bondstone.Messaging;

public sealed record MessageTypeRegistration(Type ClrType, string MessageTypeName);
