using ModularTemplate.Products.Products.Events;
using ModularTemplate.SharedKernel.Domain;
using ModularTemplate.SharedKernel.Extensions;

namespace ModularTemplate.Products.Products;

public sealed class Product : AggregateRoot<Guid>
{
    private Product(
        Guid id,
        string name,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        Name = name.TrimRequired(nameof(name));
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;

        RaiseDomainEvent(new ProductCreatedDomainEvent(id, Name));
    }

    private Product()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Product Create(string name)
    {
        return new Product(Guid.NewGuid(), name, DateTimeOffset.UtcNow);
    }

    public static Product Create(Guid productId, string name)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product id must not be empty.", nameof(productId));
        }

        return new Product(productId, name, DateTimeOffset.UtcNow);
    }

    public void Rename(string name)
    {
        Name = name.TrimRequired(nameof(name));
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
