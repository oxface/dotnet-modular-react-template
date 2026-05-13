using ModularTemplate.SharedKernel.Domain;

namespace ModularTemplate.Identity.Users.Events;

[DomainEventType(
    "identity.local-user",
    "identity.local-user-created",
    1)]
public sealed record LocalUserCreatedDomainEvent(
    Guid LocalUserId,
    string Provider,
    string Subject) : DomainEvent;
