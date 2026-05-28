using Microsoft.EntityFrameworkCore;
using ModularTemplate.Infrastructure.Outbox;
using ModularTemplate.Infrastructure.Persistence;
using Npgsql;
using Rebus.Handlers;

namespace ModularTemplate.Infrastructure.Transport;

public sealed class RebusDurableTransportHandler(
    IEnumerable<IModuleDbContext> moduleContexts) : IHandleMessages<DurableTransportEnvelope>
{
    public async Task Handle(DurableTransportEnvelope message)
    {
        IModuleDbContext? targetContext = moduleContexts
            .FirstOrDefault(ctx => string.Equals(
                ctx.ModuleName, message.TargetModule, StringComparison.Ordinal));

        if (targetContext is null)
        {
            throw new InvalidOperationException(
                $"Durable message target module '{message.TargetModule}' is not registered.");
        }

        try
        {
            targetContext.InboxMessages.Add(InboxMessage.Create(
                message.MessageId,
                message.MessageKind,
                message.MessageType,
                message.SourceModule,
                message.TargetModule,
                message.CorrelationId,
                message.CausationId,
                message.OperationId,
                idempotencyKey: null,
                message.Payload,
                message.MetadataJson,
                message.MaxAttempts));

            await targetContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            targetContext.ChangeTracker.Clear();
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        return exception.GetBaseException() is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation;
    }
}
