using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ModularTemplate.Infrastructure.Outbox;
using ModularTemplate.Infrastructure.Persistence;
using Rebus.Handlers;

namespace ModularTemplate.Infrastructure.Transport;

public sealed class RebusDurableTransportHandler(
    IEnumerable<IModuleDbContext> moduleContexts,
    IOptions<DurableMessagingOptions> options) : IHandleMessages<DurableTransportEnvelope>
{
    private readonly DurableMessagingOptions _options = options.Value;

    public async Task Handle(DurableTransportEnvelope message)
    {
        IModuleDbContext? targetContext = moduleContexts
            .FirstOrDefault(ctx => string.Equals(
                ctx.ModuleName, message.TargetModule, StringComparison.Ordinal));

        if (targetContext is null)
        {
            return;
        }

        bool exists = await targetContext.InboxMessages.AnyAsync(
            x => x.MessageId == message.MessageId && x.TargetModule == message.TargetModule);

        if (exists)
        {
            return;
        }

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
            maxAttempts: Math.Max(message.MaxAttempts, _options.MaxAttempts)));

        await targetContext.SaveChangesAsync();
    }
}
