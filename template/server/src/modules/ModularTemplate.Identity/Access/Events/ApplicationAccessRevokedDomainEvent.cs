using Bondstone.Domain;
using ModularTemplate.SharedKernel.Domain;

namespace ModularTemplate.Identity.Access.Events;

[DomainEventType(
    "identity.application-access",
    "identity.application-access-revoked",
    1)]
public sealed record ApplicationAccessRevokedDomainEvent(
    Guid ApplicationAccessId,
    Guid LocalUserId) : DomainEvent;
