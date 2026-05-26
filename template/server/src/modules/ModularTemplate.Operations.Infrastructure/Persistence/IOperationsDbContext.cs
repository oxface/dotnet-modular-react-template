using Microsoft.EntityFrameworkCore;
using ModularTemplate.Operations.Operations;

namespace ModularTemplate.Operations.Infrastructure.Persistence;

public interface IOperationsDbContext
{
    DbSet<Operation> Operations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
