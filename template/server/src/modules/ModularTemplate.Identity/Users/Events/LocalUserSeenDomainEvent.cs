using Bondstone.Domain;
using ModularTemplate.SharedKernel.Domain;

namespace ModularTemplate.Identity.Users.Events;

[DomainEventType(
    "identity.local-user",
    "identity.local-user-seen",
    1)]
public sealed record LocalUserSeenDomainEvent(
    Guid LocalUserId,
    string Provider,
    string Subject) : DomainEvent;
