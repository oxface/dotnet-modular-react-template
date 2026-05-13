using Mediator;
using ModularTemplate.Host.Authorization;
using ModularTemplate.Host.Features.CurrentUser;
using ModularTemplate.Identity;
using ModularTemplate.Identity.CurrentUser;
using ModularTemplate.Identity.Infrastructure;
using ModularTemplate.Identity.Infrastructure.Persistence;
using ModularTemplate.Persistence.Transactions;

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
                typeof(ResolveCurrentUserCommand).Assembly,
                typeof(ApplicationAccessRepository).Assembly
            ];
            options.PipelineBehaviors =
            [
                typeof(RequestValidationBehavior<,>),
                typeof(CommandTransactionBehavior<,>)
            ];
        });

        return services;
    }

    public static IServiceCollection AddModularTemplateModules(this IServiceCollection services)
    {
        services.AddIdentityModule();
        services.AddIdentityInfrastructure();
        services.AddApplicationAccessAuthorization();

        return services;
    }

    public static IEndpointRouteBuilder MapModularTemplateModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCurrentUserEndpoint();

        return endpoints;
    }
}
