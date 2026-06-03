namespace ModularTemplate.Products.Contracts.Products;

public interface IProductsQueries
{
    Task<ProductDetails?> GetProductAsync(Guid productId, CancellationToken cancellationToken);
}
