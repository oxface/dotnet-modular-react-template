using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using ModularTemplate.Infrastructure.Outbox;
using ModularTemplate.Infrastructure.Persistence.DomainEvents;
using ModularTemplate.SharedKernel.Domain;
using ModularTemplate.SharedKernel.Messaging;

namespace ModularTemplate.Infrastructure.Persistence;

/// <summary>
/// Saves the single module context changed in the current scope.
/// Domain events and outbox messages are persisted in the same module transaction
/// as aggregate changes.
/// </summary>
public sealed class ModuleUnitOfWork(
    IEnumerable<IModuleDbContext> moduleContexts,
    IServiceProvider serviceProvider,
    IMessageTypeRegistry messageTypeRegistry)
    : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        IModuleDbContext[] contexts = moduleContexts.ToArray();

        IModuleDbContext[] changedContexts = GetChangedContexts(contexts);
        EnsureSingleChangedContext(changedContexts);

        if (changedContexts.Length == 1)
        {
            await SaveAsync(changedContexts[0], cancellationToken);
        }
    }

    public async Task SaveChangesTransactionalAsync(CancellationToken cancellationToken = default)
    {
        IModuleDbContext[] contexts = moduleContexts.ToArray();
        bool hasActiveTransaction = contexts.Any(ctx => ctx.Database.CurrentTransaction is not null);

        if (hasActiveTransaction)
        {
            IModuleDbContext[] changedContexts = GetChangedContexts(contexts);
            EnsureSingleChangedContext(changedContexts);

            if (changedContexts.Length == 1)
            {
                await SaveAsync(changedContexts[0], cancellationToken);
            }

            return;
        }

        IModuleDbContext[] contextsWithChanges = GetChangedContexts(contexts);
        EnsureSingleChangedContext(contextsWithChanges);

        if (contextsWithChanges.Length == 0)
        {
            return;
        }

        IModuleDbContext changedContext = contextsWithChanges[0];
        IDbContextTransaction? transaction = null;

        try
        {
            transaction = await changedContext.Database.BeginTransactionAsync(cancellationToken);

            await SaveAsync(changedContext, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private static IModuleDbContext[] GetChangedContexts(IEnumerable<IModuleDbContext> contexts)
    {
        return contexts
            .Where(ctx => ctx.ChangeTracker.HasChanges())
            .ToArray();
    }

    private static void EnsureSingleChangedContext(IReadOnlyCollection<IModuleDbContext> changedContexts)
    {
        if (changedContexts.Count <= 1)
        {
            return;
        }

        string modules = string.Join(", ", changedContexts.Select(ctx => ctx.ModuleName).Order());

        throw new InvalidOperationException(
            "The current unit of work changed more than one module DbContext. Cross-module work must use " +
            $"module contracts or durable messaging instead of directly mutating multiple modules. Changed modules: {modules}.");
    }

    private async Task SaveAsync(IModuleDbContext ctx, CancellationToken cancellationToken)
    {
        Guid correlationId = Guid.NewGuid();

        CaptureDomainEvents(ctx, correlationId);
        await ctx.SaveChangesAsync(cancellationToken);
    }

    private void CaptureDomainEvents(
        IModuleDbContext ctx,
        Guid correlationId)
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

                PublishIntegrationEvents(ctx, domainEvent, correlationId);
            }
        }
    }

    private void PublishIntegrationEvents(
        IModuleDbContext ctx,
        IDomainEvent domainEvent,
        Guid correlationId)
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
    }
}
