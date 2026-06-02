using Bondstone.Domain;
using ModularTemplate.SharedKernel.Domain;

namespace ModularTemplate.Identity.Access.Events;

[DomainEventType(
    "identity.application-access",
    "identity.application-access-granted",
    1)]
public sealed record ApplicationAccessGrantedDomainEvent(
    Guid ApplicationAccessId,
    Guid LocalUserId) : DomainEvent;
