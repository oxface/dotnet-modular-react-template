using Microsoft.Extensions.DependencyInjection;
using ModularTemplate.Identity.Authorization;
using ModularTemplate.Identity.CurrentUser;
using ModularTemplate.Identity.Contracts.Authorization;
using ModularTemplate.Identity.Contracts.CurrentUser;

namespace ModularTemplate.Identity;

public static class IdentityModuleConfiguration
{
    public static IServiceCollection AddIdentityApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ICurrentUserProvider, CurrentUserProvider>();
        services.AddScoped<IApplicationAccessAuthorizer, ApplicationAccessAuthorizer>();

        return services;
    }
}
