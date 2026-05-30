using Mediator;
using ModularTemplate.Host.Authorization;
using ModularTemplate.Host.Features.CurrentUser;
using ModularTemplate.Identity.CurrentUser;
using ModularTemplate.Identity.Infrastructure;
using ModularTemplate.Identity.Infrastructure.Persistence;
using ModularTemplate.Operations.Infrastructure;
using ModularTemplate.Infrastructure.Persistence.Transactions;

namespace ModularTemplate.Host.Configuration;

public static class ModuleConfiguration
{
    public static IServiceCollection AddModularTemplateMediator(this IServiceCollection services)
    {
        services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.Assemblies =
            [
                typeof(SynchronizeCurrentUserCommand),
                typeof(ApplicationAccessRepository)
            ];
            options.PipelineBehaviors =
            [
                typeof(RequestValidationBehavior<,>),
                typeof(ModuleUnitOfWorkBehavior<,>)
            ];
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
