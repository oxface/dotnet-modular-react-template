using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModularTemplate.Identity.Access;
using ModularTemplate.Identity.Infrastructure;
using ModularTemplate.Identity.Infrastructure.Persistence;
using ModularTemplate.Operations.Infrastructure;
using ModularTemplate.Infrastructure.Persistence.Transactions;
using ModularTemplate.Infrastructure.Transport;

namespace ModularTemplate.Migrator;

public static class MigratorComposition
{
    public static IHostApplicationBuilder AddMigratorComposition(this IHostApplicationBuilder builder)
    {
        builder.AddTransport();
        builder.Services.AddIdentityModule();
        builder.Services.AddOperationsModule();

        builder.Services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.Assemblies =
            [
                typeof(GrantInitialAdminAccessCommand),
                typeof(ApplicationAccessRepository)
            ];
            options.PipelineBehaviors =
            [
                typeof(ModuleUnitOfWorkBehavior<,>)
            ];
        });

        return builder;
    }
}
