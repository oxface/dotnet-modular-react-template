using Microsoft.Extensions.DependencyInjection;
using ModularTemplate.Infrastructure.Persistence.Transactions;

namespace ModularTemplate.Host.Tests.Support;

internal static class TestServiceCollectionExtensions
{
    public static void RemoveModuleUnitOfWorkBehaviors(this IServiceCollection services)
    {
        for (int index = services.Count - 1; index >= 0; index--)
        {
            if (IsModuleUnitOfWorkBehavior(services[index].ImplementationType))
            {
                services.RemoveAt(index);
            }
        }
    }

    private static bool IsModuleUnitOfWorkBehavior(Type? implementationType)
    {
        return implementationType is not null
            && implementationType.IsGenericType
            && implementationType.GetGenericTypeDefinition() == typeof(ModuleUnitOfWorkBehavior<,>);
    }
}
