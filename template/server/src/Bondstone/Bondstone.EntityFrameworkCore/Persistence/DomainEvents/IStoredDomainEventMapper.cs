using Bondstone.Domain;

namespace Bondstone.EntityFrameworkCore.Persistence.DomainEvents;

public interface IStoredDomainEventMapper
{
    StoredDomainEvent Map(IDomainEvent domainEvent, string aggregateId);
}
