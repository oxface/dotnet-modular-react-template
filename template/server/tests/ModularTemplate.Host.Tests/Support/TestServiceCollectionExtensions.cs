using Microsoft.Extensions.DependencyInjection;
using Bondstone.Commands;

namespace ModularTemplate.Host.Tests.Support;

internal static class TestServiceCollectionExtensions
{
    public static void RemoveModuleCommandPipelineBehaviors(this IServiceCollection services)
    {
        for (int index = services.Count - 1; index >= 0; index--)
        {
            if (services[index].ServiceType.IsGenericType
                && services[index].ServiceType.GetGenericTypeDefinition() == typeof(IModuleCommandPipelineBehavior<,>))
            {
                services.RemoveAt(index);
            }
        }
    }
}
