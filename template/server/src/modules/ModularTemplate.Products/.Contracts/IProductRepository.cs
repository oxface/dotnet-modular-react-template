using ModularTemplate.Products.Products;

namespace ModularTemplate.Products;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid productId, CancellationToken cancellationToken);

    void Add(Product product);
}
