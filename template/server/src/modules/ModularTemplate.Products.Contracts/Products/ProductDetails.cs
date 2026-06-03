namespace ModularTemplate.Products.Contracts.Products;

public sealed record ProductDetails(
    Guid ProductId,
    string Name,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? MetadataJson);
