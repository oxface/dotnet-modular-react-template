using System.Text.Json;
using Microsoft.Extensions.Options;
using ModularTemplate.SharedKernel.Extensions;
using ModularTemplate.SharedKernel.Messaging;

namespace ModularTemplate.Infrastructure.Outbox;

public sealed class DurableCommandSender(
    IEnumerable<IOutboxWriter> outboxWriters,
    IMessageTypeRegistry messageTypeRegistry,
    IOptions<DurableMessagingOptions> options)
    : IDurableCommandSender
{
    private readonly DurableMessagingOptions _options = options.Value;

    public CommandSubmission Send<TCommand>(
        TCommand command,
        DurableCommandSubmissionOptions options)
        where TCommand : IDurableCommand
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.SourceModule);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.TargetModule);

        string sourceModule = NormalizeModule(options.SourceModule, nameof(options.SourceModule));
        string targetModule = NormalizeModule(options.TargetModule, nameof(options.TargetModule));
        ValidateConfiguredModule(sourceModule, nameof(options.SourceModule));
        ValidateConfiguredModule(targetModule, nameof(options.TargetModule));

        IOutboxWriter writer = ResolveWriter(sourceModule);
        Guid submissionId = Guid.NewGuid();
        Guid correlationId = options.CorrelationId ?? submissionId;
        string messageType = messageTypeRegistry.GetMessageTypeName(command.GetType());
        string payload = JsonSerializer.Serialize(command, command.GetType());
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
            options.Metadata,
            maxAttempts));

        return new CommandSubmission(
            submissionId,
            options.OperationId,
            CommandSubmissionStatus.Accepted);
    }

    private IOutboxWriter ResolveWriter(string sourceModule)
    {
        IOutboxWriter[] matchingWriters = outboxWriters
            .Where(writer => string.Equals(writer.ModuleName, sourceModule, StringComparison.Ordinal))
            .ToArray();

        return matchingWriters.Length switch
        {
            1 => matchingWriters[0],
            0 => throw new InvalidOperationException(
                $"No outbox writer is registered for source module '{sourceModule}'."),
            _ => throw new InvalidOperationException(
                $"Multiple outbox writers are registered for source module '{sourceModule}'."),
        };
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

    private static string NormalizeModule(string moduleName, string parameterName)
    {
        return moduleName.TrimRequired(parameterName);
    }
}
