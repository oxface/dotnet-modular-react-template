using System.Diagnostics;

namespace Bondstone.Messaging;

public static class BondstoneDiagnostics
{
    public const string ActivitySourceName = "Bondstone";
    public const string TraceParentHeader = "traceparent";
    public const string TraceStateHeader = "tracestate";
    public const string BaggageHeader = "baggage";
    public const string OperationIdBaggageKey = "bondstone.operation_id";
    public const string CausationIdBaggageKey = "bondstone.causation_id";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly DistributedContextPropagator W3CPropagator =
        DistributedContextPropagator.CreateW3CPropagator();

    public static Activity? StartActivity(string name, ActivityKind kind)
    {
        Activity? activity = ActivitySource.StartActivity(name, kind);
        if (activity is not null || Activity.Current is not null)
        {
            return activity;
        }

        return new Activity(name)
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
    }

    public static Activity? StartConsumerActivity(
        string name,
        string? traceParent,
        string? traceState)
    {
        if (!string.IsNullOrWhiteSpace(traceParent)
            && ActivityContext.TryParse(traceParent, traceState, out ActivityContext parentContext))
        {
            Activity? activity = ActivitySource.StartActivity(name, ActivityKind.Consumer, parentContext);
            if (activity is not null)
            {
                return activity;
            }

            return new Activity(name)
                .SetParentId(traceParent)
                .Start();
        }

        return StartActivity(name, ActivityKind.Consumer);
    }

    public static Guid? CreateCorrelationId(Activity? activity)
    {
        if (activity is null)
        {
            return null;
        }

        string traceId = activity.TraceId.ToHexString();
        if (string.IsNullOrWhiteSpace(traceId) || traceId.All(static c => c == '0'))
        {
            return null;
        }

        return new Guid(Convert.FromHexString(traceId));
    }

    public static Guid? GetCurrentBaggageGuid(string key)
    {
        string? value = Activity.Current?.GetBaggageItem(key);
        return Guid.TryParse(value, out Guid parsedValue)
            ? parsedValue
            : null;
    }

    public static void InjectTraceHeaders(Activity activity, IDictionary<string, string> headers)
    {
        W3CPropagator.Inject(activity, headers, static (carrier, fieldName, fieldValue) =>
        {
            if (carrier is IDictionary<string, string> carrierHeaders
                && !string.IsNullOrWhiteSpace(fieldValue))
            {
                carrierHeaders[fieldName] = fieldValue;
            }
        });
    }

    public static IEnumerable<KeyValuePair<string, string?>> ExtractBaggage(IReadOnlyDictionary<string, string> headers)
    {
        return W3CPropagator.ExtractBaggage(headers, static (
            object? carrier,
            string fieldName,
            out string? fieldValue,
            out IEnumerable<string>? fieldValues) =>
        {
            fieldValues = null;
            if (carrier is IReadOnlyDictionary<string, string> carrierHeaders
                && carrierHeaders.TryGetValue(fieldName, out string? headerValue))
            {
                fieldValue = headerValue;
                return;
            }

            fieldValue = null;
        }) ?? [];
    }
}
