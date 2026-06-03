using Microsoft.EntityFrameworkCore;
using ModularTemplate.Products.Contracts.Products;

namespace ModularTemplate.Products.Infrastructure.Persistence;

public sealed class ProductsQueries(ProductsDbContext dbContext) : IProductsQueries
{
    public async Task<ProductDetails?> GetProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        return await dbContext.Products
            .AsNoTracking()
            .Where(x => x.Id == productId)
            .Select(x => new ProductDetails(
                x.Id,
                x.Name,
                x.CreatedAtUtc,
                x.UpdatedAtUtc,
                x.MetadataJson))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
