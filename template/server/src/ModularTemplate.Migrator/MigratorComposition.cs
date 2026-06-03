using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Bondstone.Commands;
using ModularTemplate.Identity.Access;
using ModularTemplate.Identity.Infrastructure;
using ModularTemplate.Identity.Infrastructure.Persistence;
using ModularTemplate.Products.Infrastructure;
using Bondstone.Transport.Rebus;

namespace ModularTemplate.Migrator;

public static class MigratorComposition
{
    public static IHostApplicationBuilder AddMigratorComposition(this IHostApplicationBuilder builder)
    {
        builder.AddRebusTransport(transport =>
            transport.UsePostgresInternalTransport(builder.Configuration.GetSection("Messaging:Rebus")));
        builder.Services.AddIdentityModule();
        builder.Services.AddProductsModule();

        Type[] commandAssemblyMarkers =
        [
            typeof(GrantInitialAdminAccessCommand),
            typeof(ApplicationAccessRepository)
        ];

        builder.Services.AddModuleCommands(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
            foreach (Type marker in commandAssemblyMarkers)
            {
                options.AssemblyMarkers.Add(marker);
            }

            options.PipelineBehaviors.Add(typeof(ModuleCommandDiagnosticsBehavior<,>));
        });

        return builder;
    }
}
