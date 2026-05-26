using System.Text.Json;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using ModularTemplate.Outbox.DomainEvents;
using ModularTemplate.SharedKernel.Domain;
using ModularTemplate.SharedKernel.Messaging;

namespace ModularTemplate.Outbox.Transactions;

/// <summary>
/// Opens a per-context transaction for each module context that has pending changes.
/// After the module saves, domain events are captured from tracked aggregates, persisted
/// to the module's own domain-events table, and mapped to outbox messages written to the
/// same module's outbox table.
/// </summary>
public sealed class CommandTransactionBehavior<TCommand, TResponse>(
    IEnumerable<IModuleDbContext> moduleContexts,
    IServiceProvider serviceProvider,
    IMessageTypeRegistry messageTypeRegistry)
    : IPipelineBehavior<TCommand, TResponse>
    where TCommand : IBaseCommand
{
    public async ValueTask<TResponse> Handle(
        TCommand message,
        MessageHandlerDelegate<TCommand, TResponse> next,
        CancellationToken cancellationToken)
    {
        // Nested: already inside a transaction — just save, don't commit.
        bool hasActiveTransaction = moduleContexts.Any(
            ctx => ctx.Database.CurrentTransaction is not null);

        if (hasActiveTransaction)
        {
            TResponse nestedResponse = await next(message, cancellationToken);
            await SaveAllAsync(cancellationToken);
            return nestedResponse;
        }

        // Begin a separate transaction per context that has changes.
        // Contexts without changes are skipped to avoid unnecessary overhead.
        TResponse response = await next(message, cancellationToken);

        var transactions = new List<(IModuleDbContext Context, IDbContextTransaction Transaction)>();

        try
        {
            foreach (IModuleDbContext ctx in moduleContexts)
            {
                if (ctx.ChangeTracker.HasChanges())
                {
                    IDbContextTransaction tx =
                        await ctx.Database.BeginTransactionAsync(cancellationToken);
                    transactions.Add((ctx, tx));
                }
            }

            await SaveAllAsync(cancellationToken);

            foreach ((_, IDbContextTransaction tx) in transactions)
            {
                await tx.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            foreach ((_, IDbContextTransaction tx) in transactions)
            {
                await tx.RollbackAsync(cancellationToken);
            }

            throw;
        }
        finally
        {
            foreach ((_, IDbContextTransaction tx) in transactions)
            {
                await tx.DisposeAsync();
            }
        }

        return response;
    }

    private async Task SaveAllAsync(CancellationToken cancellationToken)
    {
        Guid correlationId = Guid.NewGuid();

        foreach (IModuleDbContext ctx in moduleContexts)
        {
            await CaptureDomainEventsAsync(ctx, correlationId, cancellationToken);
            await ctx.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task CaptureDomainEventsAsync(
        IModuleDbContext ctx,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var entries = ctx.ChangeTracker
            .Entries<IAggregateRoot>()
            .Where(x => x.Entity.DomainEvents.Count > 0)
            .Select(x => new
            {
                Aggregate = x.Entity,
                Events = x.Entity.DequeueDomainEvents(),
            })
            .ToArray();

        foreach (var entry in entries)
        {
            foreach (IDomainEvent domainEvent in entry.Events)
            {
                ctx.DomainEvents.Add(
                    StoredDomainEvent.FromDomainEvent(
                        domainEvent,
                        entry.Aggregate.Id.ToString() ?? string.Empty));

                await PublishIntegrationEventsAsync(ctx, domainEvent, correlationId, cancellationToken);
            }
        }
    }

    private async Task PublishIntegrationEventsAsync(
        IModuleDbContext ctx,
        IDomainEvent domainEvent,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        Type mapperInterface = typeof(IIntegrationEventMapper<>).MakeGenericType(domainEvent.GetType());

        foreach (IIntegrationEventMapper mapper in
            serviceProvider.GetServices(mapperInterface).OfType<IIntegrationEventMapper>())
        {
            IReadOnlyCollection<IIntegrationEvent> integrationEvents = mapper.Map(domainEvent);

            foreach (IIntegrationEvent integrationEvent in integrationEvents)
            {
                string messageType = messageTypeRegistry.GetMessageTypeName(integrationEvent.GetType());
                string payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType());

                ctx.OutboxMessages.Add(OutboxMessage.Create(
                    messageId: Guid.NewGuid(),
                    messageKind: MessageKind.Event,
                    messageType,
                    sourceModule: mapper.SourceModule,
                    targetModule: null,
                    correlationId,
                    causationId: domainEvent.EventId,
                    operationId: null,
                    payload));
            }
        }

        await Task.CompletedTask;
    }
}
