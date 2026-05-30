using System.Text.Json;
using Microsoft.Extensions.Options;
using ModularTemplate.Infrastructure.Persistence;
using ModularTemplate.Infrastructure.Transport;
using ModularTemplate.SharedKernel.Extensions;
using ModularTemplate.SharedKernel.Messaging;

namespace ModularTemplate.Infrastructure.Outbox;

public sealed class DurableCommandSender(
    IModulePersistenceResolver persistenceResolver,
    IEnumerable<ModuleMessageHandlerRegistration> messageHandlerRegistrations,
    IMessageTypeRegistry messageTypeRegistry,
    IModuleUnitOfWorkContext unitOfWorkContext,
    IOptions<DurableMessagingOptions> options)
    : IDurableCommandSender
{
    private readonly DurableMessagingOptions _options = options.Value;

    public CommandSubmission Send<TCommand>(
        TCommand command,
        DurableCommandSubmissionOptions options)
        where TCommand : IDurableCommand
    {
        if (!_options.Enabled)
        {
            throw new InvalidOperationException(
                "Durable messaging is disabled. Durable commands cannot be accepted because no dispatcher will deliver them.");
        }

        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(options);

        string sourceModule = NormalizeModule(options.SourceModule, nameof(options.SourceModule));
        string targetModule = NormalizeModule(options.TargetModule, nameof(options.TargetModule));
        ValidateConfiguredModule(sourceModule, nameof(options.SourceModule));
        ValidateConfiguredModule(targetModule, nameof(options.TargetModule));
        ValidateActiveSourceModule(sourceModule);

        IOutboxWriter writer = persistenceResolver.ResolveOutboxWriter(sourceModule);
        Type commandType = command.GetType();
        ValidateTargetModuleHandler(targetModule, commandType);
        Guid submissionId = Guid.NewGuid();
        Guid correlationId = options.CorrelationId ?? submissionId;
        string messageType = messageTypeRegistry.GetMessageTypeName(commandType);
        string payload = JsonSerializer.Serialize(command, commandType);
        int maxAttempts = options.MaxAttempts ?? _options.MaxAttempts;

        writer.Write(OutboxMessage.Create(
            messageId: submissionId,
            messageKind: MessageKind.Command,
            messageType,
            sourceModule,
            targetModule,
            correlationId,
            causationId: options.CausationId,
            operationId: options.OperationId,
            payload,
            maxAttempts: maxAttempts));

        return new CommandSubmission(
            submissionId,
            options.OperationId,
            CommandSubmissionStatus.Accepted);
    }

    private void ValidateConfiguredModule(string moduleName, string optionName)
    {
        if (_options.Modules.ContainsTrimmedOrdinal(moduleName))
        {
            return;
        }

        throw new InvalidOperationException(
            $"{optionName} '{moduleName}' is not listed in Messaging:Modules.");
    }

    private void ValidateActiveSourceModule(string sourceModule)
    {
        string? currentModuleName = unitOfWorkContext.CurrentModuleName;
        if (string.IsNullOrWhiteSpace(currentModuleName))
        {
            throw new InvalidOperationException(
                "Durable commands must be sent inside a module unit of work so the outbox row is committed with module state.");
        }

        if (string.Equals(currentModuleName, sourceModule, StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Durable command source module '{sourceModule}' does not match the active module unit of work '{currentModuleName}'.");
    }

    private void ValidateTargetModuleHandler(string targetModule, Type commandType)
    {
        ModuleMessageHandlerRegistration[] matchingRegistrations = messageHandlerRegistrations
            .Where(registration =>
                string.Equals(registration.ModuleName, targetModule, StringComparison.Ordinal)
                && registration.MessageType == commandType)
            .ToArray();

        if (matchingRegistrations.Length == 1)
        {
            return;
        }

        if (matchingRegistrations.Length == 0)
        {
            throw new InvalidOperationException(
                $"No module message handler is registered for durable command '{commandType.FullName}' " +
                $"in target module '{targetModule}'.");
        }

        throw new InvalidOperationException(
            $"Multiple module message handlers are registered for durable command '{commandType.FullName}' " +
            $"in target module '{targetModule}'.");
    }

    private static string NormalizeModule(string moduleName, string parameterName)
    {
        return moduleName.TrimRequired(parameterName);
    }
}
