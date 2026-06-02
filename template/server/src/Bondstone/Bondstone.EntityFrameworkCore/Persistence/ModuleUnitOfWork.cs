using System.Text.Json;
using System.Diagnostics;
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
    private Guid? _transactionCorrelationId;
    private bool _failed;

    public string ModuleName => context.ModuleName;

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfFailed();

        if (context.ChangeTracker.HasChanges() || HasDomainEvents())
        {
            try
            {
                await SaveAsync(cancellationToken);
            }
            catch
            {
                MarkFailed();
                throw;
            }
        }
    }

    public async ValueTask<T> ExecuteTransactionalAsync<T>(
        Func<CancellationToken, ValueTask<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ThrowIfFailed();

        using IDisposable moduleScope = unitOfWorkContext.StartModuleScope(context.ModuleName);

        if (context.Database.CurrentTransaction is not null)
        {
            using Activity? activity = BondstoneDiagnostics.StartActivity(
                $"Bondstone {context.ModuleName} unit of work",
                ActivityKind.Internal);

            try
            {
                T result = await operation(cancellationToken);
                await SaveChangesAsync(cancellationToken);
                return result;
            }
            catch
            {
                MarkFailed();
                throw;
            }
        }

        IDbContextTransaction? transaction = null;
        Guid? previousTransactionCorrelationId = _transactionCorrelationId;

        try
        {
            using Activity? activity = BondstoneDiagnostics.StartActivity(
                $"Bondstone {context.ModuleName} unit of work",
                ActivityKind.Internal);
            _transactionCorrelationId = BondstoneDiagnostics.CreateCorrelationId(Activity.Current) ?? Guid.NewGuid();
            transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            T result = await operation(cancellationToken);
            await SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            MarkFailed();

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

            _transactionCorrelationId = previousTransactionCorrelationId;
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        Guid correlationId = _transactionCorrelationId
            ?? BondstoneDiagnostics.CreateCorrelationId(Activity.Current)
            ?? Guid.NewGuid();
        IReadOnlyCollection<IAggregateRoot> capturedAggregates = CaptureDomainEvents(correlationId);

        await context.SaveChangesAsync(cancellationToken);
        ClearDomainEvents(capturedAggregates);
    }

    private bool HasDomainEvents()
    {
        return context.ChangeTracker
            .Entries<IAggregateRoot>()
            .Any(x => x.Entity.DomainEvents.Count > 0);
    }

    private IReadOnlyCollection<IAggregateRoot> CaptureDomainEvents(Guid correlationId)
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

        return entries
            .Select(entry => entry.Aggregate)
            .Distinct()
            .ToArray();
    }

    private static void ClearDomainEvents(IReadOnlyCollection<IAggregateRoot> aggregates)
    {
        foreach (IAggregateRoot aggregate in aggregates)
        {
            aggregate.DequeueDomainEvents();
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
                    metadata: MessageTraceContext.CaptureMetadata(),
                    maxAttempts: _options.MaxAttempts));
            }
        }
    }

    private void MarkFailed()
    {
        _failed = true;
    }

    private void ThrowIfFailed()
    {
        if (!_failed)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Module unit of work '{context.ModuleName}' has failed and cannot be reused. " +
            "Retry from a fresh request or message-handling scope.");
    }
}
