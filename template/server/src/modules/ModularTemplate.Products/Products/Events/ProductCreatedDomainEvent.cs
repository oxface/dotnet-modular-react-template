using Bondstone.Domain;
using ModularTemplate.SharedKernel.Domain;

namespace ModularTemplate.Products.Products.Events;

[DomainEventType("products.product", "products.product-created", 1)]
public sealed record ProductCreatedDomainEvent(
    Guid ProductId,
    string Name) : DomainEvent;
