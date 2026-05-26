using Microsoft.Extensions.DependencyInjection;
using ModularTemplate.Outbox.Transactions;

namespace ModularTemplate.Host.Tests.Support;

internal static class TestServiceCollectionExtensions
{
    public static void RemoveCommandTransactionBehaviors(this IServiceCollection services)
    {
        for (int index = services.Count - 1; index >= 0; index--)
        {
            if (IsCommandTransactionBehavior(services[index].ImplementationType))
            {
                services.RemoveAt(index);
            }
        }
    }

    private static bool IsCommandTransactionBehavior(Type? implementationType)
    {
        return implementationType is not null
            && implementationType.IsGenericType
            && implementationType.GetGenericTypeDefinition() == typeof(CommandTransactionBehavior<,>);
    }
}
