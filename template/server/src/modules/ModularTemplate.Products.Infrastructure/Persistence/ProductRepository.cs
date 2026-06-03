using Microsoft.EntityFrameworkCore;
using ModularTemplate.Products.Products;

namespace ModularTemplate.Products.Infrastructure.Persistence;

public sealed class ProductRepository(ProductsDbContext dbContext) : IProductRepository
{
    public Task<Product?> GetByIdAsync(Guid productId, CancellationToken cancellationToken)
    {
        return dbContext.Products.SingleOrDefaultAsync(x => x.Id == productId, cancellationToken);
    }

    public void Add(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);
        dbContext.Products.Add(product);
    }
}
