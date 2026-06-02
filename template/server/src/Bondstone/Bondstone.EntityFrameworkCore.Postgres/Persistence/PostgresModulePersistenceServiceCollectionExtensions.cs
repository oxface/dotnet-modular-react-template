using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Bondstone.EntityFrameworkCore.Inbox;
using Bondstone.EntityFrameworkCore.Outbox;
using Bondstone.EntityFrameworkCore.Persistence;
using Bondstone.EntityFrameworkCore.Postgres.Inbox;
using Bondstone.EntityFrameworkCore.Postgres.Outbox;

namespace Bondstone.EntityFrameworkCore.Postgres.Persistence;

public static class PostgresModulePersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddModulePersistence<TDbContext>(
        this IServiceCollection services,
        string moduleName,
        params Type[] commandTypes)
        where TDbContext : DbContext, IModuleDbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddEntityFrameworkCoreModulePersistence<TDbContext>(moduleName, commandTypes);
        services.TryAddScoped<IInboxClaimConflictDetector, PostgresInboxClaimConflictDetector>();
        services.TryAddScoped<IOutboxDispatchLock, PostgresAdvisoryOutboxDispatchLock>();

        return services;
    }
}
