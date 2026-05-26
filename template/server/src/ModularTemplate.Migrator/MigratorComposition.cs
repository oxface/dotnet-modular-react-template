using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModularTemplate.Identity;
using ModularTemplate.Identity.CurrentUser;
using ModularTemplate.Identity.Infrastructure;
using ModularTemplate.Identity.Infrastructure.Persistence;
using ModularTemplate.Operations.Infrastructure;
using ModularTemplate.Transport;
using ModularTemplate.Outbox.Transactions;

namespace ModularTemplate.Migrator;

public static class MigratorComposition
{
    public static IHostApplicationBuilder AddMigratorComposition(this IHostApplicationBuilder builder)
    {
        builder.AddTransport();
        builder.Services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.Assemblies =
            [
                typeof(ResolveCurrentUserCommand).Assembly,
                typeof(ApplicationAccessRepository).Assembly
            ];
            options.PipelineBehaviors =
            [
                typeof(CommandTransactionBehavior<,>)
            ];
        });
        builder.Services.AddIdentityModule();
        builder.Services.AddIdentityInfrastructure();
        builder.Services.AddOperationsInfrastructure();

        return builder;
    }
}
