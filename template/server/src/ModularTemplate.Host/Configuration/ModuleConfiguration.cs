using Mediator;
using Bondstone.Commands;
using ModularTemplate.Host.Authorization;
using ModularTemplate.Host.Features.CurrentUser;
using ModularTemplate.Identity.CurrentUser;
using ModularTemplate.Identity.Infrastructure;
using ModularTemplate.Identity.Infrastructure.Persistence;
using ModularTemplate.Operations.Infrastructure;

namespace ModularTemplate.Host.Configuration;

public static class ModuleConfiguration
{
    public static IServiceCollection AddModularCommandHandling(this IServiceCollection services)
    {
        Type[] commandAssemblyMarkers =
        [
            typeof(SynchronizeCurrentUserCommand),
            typeof(ApplicationAccessRepository)
        ];

        services.AddModuleCommands(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
            foreach (Type marker in commandAssemblyMarkers)
            {
                options.AssemblyMarkers.Add(marker);
            }

            options.PipelineBehaviors.Add(typeof(RequestValidationBehavior<,>));
        });

        // Mediator's source generator must run in the composing app assembly.
        services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
        });

        return services;
    }

    public static IServiceCollection AddModularTemplateModules(this IServiceCollection services)
    {
        services.AddIdentityModule();
        services.AddOperationsModule();
        services.AddApplicationAccessAuthorization();

        return services;
    }

    public static IEndpointRouteBuilder MapModularTemplateModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCurrentUserEndpoint();
        endpoints.MapOperationsModule();

        return endpoints;
    }
}
