using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Bondstone.EntityFrameworkCore.Outbox;
using Bondstone.EntityFrameworkCore.Persistence.DomainEvents;
using Bondstone.Domain;
using Bondstone.Internal;
using Bondstone.Messaging;

namespace Bondstone.EntityFrameworkCore.Persistence;

public sealed class ModuleUnitOfWork<TDbContext>(
    TDbContext context,
    IServiceProvider serviceProvider,
    IMessageTypeRegistry messageTypeRegistry,
    IStoredDomainEventMapper storedDomainEventMapper,
    IModuleUnitOfWorkContext unitOfWorkContext,
    IOptions<DurableMessagingOptions> options)
    : IModuleUnitOfWork
    where TDbContext : DbContext, IModuleDbContext
{
    private readonly DurableMessagingOptions _options = options.Value;

    public string ModuleName => context.ModuleName;

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (context.ChangeTracker.HasChanges() || HasDomainEvents())
        {
            await SaveAsync(cancellationToken);
        }
    }

    public async ValueTask<T> ExecuteTransactionalAsync<T>(
        Func<CancellationToken, ValueTask<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        using IDisposable moduleScope = unitOfWorkContext.StartModuleScope(context.ModuleName);

        if (context.Database.CurrentTransaction is not null)
        {
            T result = await operation(cancellationToken);
            await SaveChangesAsync(cancellationToken);
            return result;
        }

        IDbContextTransaction? transaction = null;

        try
        {
            transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            T result = await operation(cancellationToken);
            await SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
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

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        Guid correlationId = Guid.NewGuid();

        CaptureDomainEvents(correlationId);
        await context.SaveChangesAsync(cancellationToken);
    }

    private bool HasDomainEvents()
    {
        return context.ChangeTracker
            .Entries<IAggregateRoot>()
            .Any(x => x.Entity.DomainEvents.Count > 0);
    }

    private void CaptureDomainEvents(Guid correlationId)
    {
        var entries = context.ChangeTracker
            .Entries<IAggregateRoot>()
            .Where(x => x.Entity.DomainEvents.Count > 0)
            .Select(x => new
            {
                Aggregate = x.Entity,
                Events = x.Entity.DomainEvents.ToArray(),
            })
            .ToArray();

        foreach (var entry in entries)
        {
            foreach (IDomainEvent domainEvent in entry.Events)
            {
                context.DomainEvents.Add(
                    storedDomainEventMapper.Map(
                        domainEvent,
                        entry.Aggregate.Id.ToString() ?? string.Empty));

                PublishIntegrationEvents(domainEvent, correlationId);
            }
        }

        foreach (var entry in entries)
        {
            entry.Aggregate.DequeueDomainEvents();
        }
    }

    private void PublishIntegrationEvents(
        IDomainEvent domainEvent,
        Guid correlationId)
    {
        Type mapperInterface = typeof(IIntegrationEventMapper<>).MakeGenericType(domainEvent.GetType());

        foreach (IIntegrationEventMapper mapper in
            serviceProvider.GetServices(mapperInterface).OfType<IIntegrationEventMapper>())
        {
            string mapperSourceModule = mapper.SourceModule.TrimToNull() ?? string.Empty;
            if (!string.Equals(mapperSourceModule, context.ModuleName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Integration event mapper source module '{mapperSourceModule}' does not match " +
                    $"the changed module context '{context.ModuleName}'.");
            }

            IReadOnlyCollection<IIntegrationEvent> integrationEvents = mapper.Map(domainEvent);

            foreach (IIntegrationEvent integrationEvent in integrationEvents)
            {
                string messageType = messageTypeRegistry.GetMessageTypeName(integrationEvent.GetType());
                string payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType());

                context.OutboxMessages.Add(OutboxMessage.Create(
                    messageId: Guid.NewGuid(),
                    messageKind: MessageKind.Event,
                    messageType,
                    sourceModule: context.ModuleName,
                    targetModule: null,
                    correlationId,
                    causationId: domainEvent.EventId,
                    operationId: null,
                    payload,
                    maxAttempts: _options.MaxAttempts));
            }
        }
    }
}
