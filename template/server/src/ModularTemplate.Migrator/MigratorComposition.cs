using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModularTemplate.Identity.Access;
using ModularTemplate.Identity.Infrastructure;
using ModularTemplate.Identity.Infrastructure.Persistence;
using ModularTemplate.Operations.Infrastructure;
using Bondstone.Mediator.Persistence.Transactions;
using Bondstone.Transport.Rebus;

namespace ModularTemplate.Migrator;

public static class MigratorComposition
{
    public static IHostApplicationBuilder AddMigratorComposition(this IHostApplicationBuilder builder)
    {
        builder.AddRebusTransport(transport =>
            transport.UsePostgresInternalTransport(builder.Configuration.GetSection("Messaging:Rebus")));
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
