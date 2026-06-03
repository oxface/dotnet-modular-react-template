namespace ModularTemplate.Products.Products;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid productId, CancellationToken cancellationToken);

    void Add(Product product);
}
