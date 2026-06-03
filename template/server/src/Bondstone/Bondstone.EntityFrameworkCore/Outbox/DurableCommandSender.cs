using System.Text.Json;
using System.Diagnostics;
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
        Guid? durableOperationId = null,
        Guid? causationId = null,
        int? maxAttempts = null,
        string? partitionKey = null)
        where TCommand : IDurableCommand
    {
        ArgumentNullException.ThrowIfNull(command);

        string sourceModule = GetActiveSourceModule();
        string normalizedTargetModule = targetModule.TrimRequired(nameof(targetModule));

        IOutboxWriter writer = persistenceResolver.ResolveOutboxWriter(sourceModule);
        Type commandType = command.GetType();
        ValidateTargetModuleHandler(normalizedTargetModule, commandType);
        Guid submissionId = Guid.NewGuid();
        Guid correlationId = BondstoneDiagnostics.CreateCorrelationId(Activity.Current) ?? submissionId;
        Guid? messageCausationId = causationId ?? BondstoneDiagnostics.GetCurrentBaggageGuid(BondstoneDiagnostics.CausationIdBaggageKey);
        Guid? messageDurableOperationId = durableOperationId ?? BondstoneDiagnostics.GetCurrentBaggageGuid(BondstoneDiagnostics.DurableOperationIdBaggageKey);
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
            messageCausationId,
            messageDurableOperationId,
            payload,
            partitionKey,
            metadata: MessageTraceContext.CaptureMetadata(),
            maxAttempts: messageMaxAttempts));

        return new CommandSubmission(
            submissionId,
            messageDurableOperationId,
            CommandSubmissionStatus.Accepted);
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
