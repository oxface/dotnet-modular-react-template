namespace Bondstone.Messaging;

internal static class ModuleMessageHandlerRegistrationPolicy
{
    public static bool ShouldRegister(
        string moduleName,
        ModuleMessageHandlerRegistrationDescriptor descriptor,
        IReadOnlyCollection<ModuleMessageHandlerRegistration> existingRegistrations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(existingRegistrations);

        ModuleMessageHandlerRegistration[] existingForMessage = existingRegistrations
            .Where(registration =>
                string.Equals(registration.ModuleName, moduleName, StringComparison.Ordinal)
                && registration.MessageType == descriptor.MessageType)
            .ToArray();

        if (existingForMessage.Any(registration => registration.HandlerType == descriptor.HandlerType))
        {
            return false;
        }

        if (existingForMessage.Length == 0)
        {
            return true;
        }

        if (!typeof(IIntegrationEvent).IsAssignableFrom(descriptor.MessageType))
        {
            throw new InvalidOperationException(
                $"Module '{moduleName}' already has a durable command handler for '{descriptor.MessageType.FullName}'. " +
                "Durable commands must have exactly one handler in the target module.");
        }

        if (string.Equals(descriptor.HandlerIdentity, descriptor.MessageIdentity, StringComparison.Ordinal)
            || existingForMessage.Any(registration =>
                string.Equals(registration.HandlerIdentity, registration.MessageIdentity, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Module '{moduleName}' has multiple integration event handlers for '{descriptor.MessageType.FullName}'. " +
                $"Each handler must declare {nameof(IntegrationEventHandlerIdentityAttribute)}.");
        }

        if (existingForMessage.Any(registration =>
                string.Equals(registration.HandlerIdentity, descriptor.HandlerIdentity, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Module '{moduleName}' already has an integration event handler identity '{descriptor.HandlerIdentity}' " +
                $"for '{descriptor.MessageType.FullName}'.");
        }

        return true;
    }
}
