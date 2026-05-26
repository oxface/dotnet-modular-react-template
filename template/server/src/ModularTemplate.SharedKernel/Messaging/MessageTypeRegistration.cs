namespace ModularTemplate.SharedKernel.Messaging;

public sealed record MessageTypeRegistration(Type ClrType, string MessageTypeName);
