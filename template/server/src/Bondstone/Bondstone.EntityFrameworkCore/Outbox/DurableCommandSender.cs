using System.Text.Json;
using Microsoft.Extensions.Options;
using Bondstone.EntityFrameworkCore.Persistence;
using Bondstone.Internal;
using Bondstone.Messaging;

namespace Bondstone.EntityFrameworkCore.Outbox;

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
        string targetModule,
        Guid? operationId = null,
        Guid? causationId = null,
        int? maxAttempts = null)
        where TCommand : IDurableCommand
    {
        ArgumentNullException.ThrowIfNull(command);

        string sourceModule = GetActiveSourceModule();
        string normalizedTargetModule = targetModule.TrimRequired(nameof(targetModule));
        ValidateConfiguredModule(sourceModule, "active source module");
        ValidateConfiguredModule(normalizedTargetModule, nameof(targetModule));

        IOutboxWriter writer = persistenceResolver.ResolveOutboxWriter(sourceModule);
        Type commandType = command.GetType();
        ValidateTargetModuleHandler(normalizedTargetModule, commandType);
        Guid submissionId = Guid.NewGuid();
        Guid correlationId = submissionId;
        string messageType = messageTypeRegistry.GetMessageTypeName(commandType);
        string payload = JsonSerializer.Serialize(command, commandType);
        int messageMaxAttempts = maxAttempts ?? _options.MaxAttempts;

        writer.Write(OutboxMessage.Create(
            messageId: submissionId,
            messageKind: MessageKind.Command,
            messageType,
            sourceModule,
            normalizedTargetModule,
            correlationId,
            causationId,
            operationId,
            payload,
            maxAttempts: messageMaxAttempts));

        return new CommandSubmission(
            submissionId,
            operationId,
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

    private string GetActiveSourceModule()
    {
        string? currentModuleName = unitOfWorkContext.CurrentModuleName;
        if (string.IsNullOrWhiteSpace(currentModuleName))
        {
            throw new InvalidOperationException(
                "Durable commands must be sent inside a module unit of work so the outbox row is committed with module state.");
        }

        return currentModuleName.TrimRequired(nameof(currentModuleName));
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

}
