using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularTemplate.SharedKernel.Messaging;

namespace ModularTemplate.Outbox;

public sealed class InboxProcessor(
    IServiceProvider serviceProvider,
    IEnumerable<IModuleDbContext> moduleContexts,
    IMessageTypeRegistry messageTypeRegistry,
    IOptions<DurableMessagingOptions> options,
    ILogger<InboxProcessor> logger)
    : IInboxProcessor
{
    private readonly DurableMessagingOptions _options = options.Value;

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return 0;
        }

        int totalProcessed = 0;

        foreach (IModuleDbContext ctx in moduleContexts)
        {
            totalProcessed += await ProcessForContextAsync(ctx, cancellationToken);
        }

        return totalProcessed;
    }

    private async Task<int> ProcessForContextAsync(
        IModuleDbContext ctx,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset staleThreshold = now - _options.LockTimeout;
        string claimToken = Guid.NewGuid().ToString("N");
        string schema = DelimitSchema(ctx.ModuleName);

        string pending = PersistedMessageStatus.Pending.ToString();
        string failed = PersistedMessageStatus.Failed.ToString();
        string processing = PersistedMessageStatus.Processing.ToString();

        // Atomically claim a batch using FOR UPDATE SKIP LOCKED to prevent double-processing.
        string claimSql =
            $$"""
            UPDATE {{schema}}.inbox_messages
            SET "Status" = 'Processing',
                "LockedAtUtc" = {0},
                "LockedBy" = {1}
            WHERE "Id" = ANY(
                SELECT "Id" FROM {{schema}}.inbox_messages
                WHERE (
                    ("Status" = {2} OR "Status" = {3})
                    AND "NextAttemptAtUtc" <= {4}
                ) OR (
                    "Status" = {5}
                    AND "LockedAtUtc" IS NOT NULL
                    AND "LockedAtUtc" < {6}
                )
                ORDER BY "ReceivedAtUtc"
                LIMIT {7}
                FOR UPDATE SKIP LOCKED
            )
            """;

        await ctx.Database.ExecuteSqlAsync(
            FormattableStringFactory.Create(
                claimSql,
                now,
                claimToken,
                pending,
                failed,
                now,
                processing,
                staleThreshold,
                _options.BatchSize),
            cancellationToken);

        IReadOnlyList<InboxMessage> pendingMessages = await ctx.InboxMessages
            .Where(x => x.LockedBy == claimToken)
            .OrderBy(x => x.ReceivedAtUtc)
            .ToArrayAsync(cancellationToken);

        if (pendingMessages.Count == 0)
        {
            return 0;
        }

        int processedCount = 0;

        foreach (InboxMessage message in pendingMessages)
        {
            try
            {
                await HandleMessageAsync(message, cancellationToken);
                message.MarkProcessed();
                processedCount++;
            }
            catch (Exception ex)
            {
                Exception rootException = ex.GetBaseException();
                logger.LogError(
                    ex,
                    "Inbox processing failed for message {MessageId} type {MessageType}",
                    message.MessageId,
                    message.MessageType);
                message.MarkFailed(rootException.Message, RetryDelays.ForAttempt);
            }
        }

        await ctx.SaveChangesAsync(cancellationToken);
        return processedCount;
    }

    private static string DelimitSchema(string schema)
    {
        if (string.IsNullOrWhiteSpace(schema)
            || schema.Any(static c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
        {
            throw new InvalidOperationException($"Invalid module schema name '{schema}'.");
        }

        return "\"" + schema + "\"";
    }

    private async Task HandleMessageAsync(InboxMessage message, CancellationToken cancellationToken)
    {
        if (!messageTypeRegistry.TryResolveClrType(message.MessageType, out Type? clrType) || clrType is null)
        {
            throw new InvalidOperationException($"Unknown message type '{message.MessageType}'.");
        }

        object payload = JsonSerializer.Deserialize(message.Payload, clrType)
            ?? throw new InvalidOperationException(
                $"Message '{message.MessageId}' payload could not be deserialized as '{clrType.FullName}'.");

        var context = new MessageContext(
            message.MessageId,
            message.SourceModule,
            message.TargetModule,
            message.CorrelationId,
            message.CausationId,
            message.OperationId,
            message.IdempotencyKey,
            message.ReceivedAtUtc);

        using IServiceScope scope = serviceProvider.CreateScope();

        if (message.MessageKind == MessageKind.Command)
        {
            Type handlerType = typeof(IDurableCommandHandler<>).MakeGenericType(clrType);
            object handler = scope.ServiceProvider.GetRequiredService(handlerType);
            await InvokeHandlerAsync(handlerType, handler, payload, context, cancellationToken);
            return;
        }

        Type eventHandlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(clrType);
        Type enumerableType = typeof(IEnumerable<>).MakeGenericType(eventHandlerType);
        var handlers = (IEnumerable<object>?)scope.ServiceProvider.GetService(enumerableType);

        if (handlers is null)
        {
            return;
        }

        foreach (object handler in handlers)
        {
            await InvokeHandlerAsync(eventHandlerType, handler, payload, context, cancellationToken);
        }
    }

    private static Task InvokeHandlerAsync(
        Type handlerType,
        object handler,
        object payload,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        const string methodName = "HandleAsync";
        var method = handlerType.GetMethod(methodName)
            ?? throw new InvalidOperationException(
                $"Handler type '{handlerType.FullName}' does not expose '{methodName}'.");

        return (Task)method.Invoke(handler, [payload, context, cancellationToken])!;
    }
}
