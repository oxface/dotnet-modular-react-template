using System.Text.Json;
using ModularTemplate.SharedKernel.Messaging;

namespace ModularTemplate.Infrastructure.Outbox;

public sealed class DurableCommandSubmitter(
    IEnumerable<IOutboxWriter> outboxWriters,
    IMessageTypeRegistry messageTypeRegistry)
    : IDurableCommandSubmitter
{
    public CommandSubmission Submit<TCommand>(
        TCommand command,
        DurableCommandSubmissionOptions options)
        where TCommand : IDurableCommand
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.SourceModule);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.TargetModule);

        IOutboxWriter writer = ResolveWriter(options.SourceModule);
        Guid submissionId = Guid.NewGuid();
        Guid correlationId = options.CorrelationId ?? submissionId;
        string messageType = messageTypeRegistry.GetMessageTypeName(command.GetType());
        string payload = JsonSerializer.Serialize(command, command.GetType());

        writer.Write(OutboxMessage.Create(
            messageId: submissionId,
            messageKind: MessageKind.Command,
            messageType,
            sourceModule: options.SourceModule,
            targetModule: options.TargetModule,
            correlationId,
            causationId: options.CausationId,
            operationId: options.OperationId,
            payload,
            options.Metadata,
            options.MaxAttempts));

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
}
