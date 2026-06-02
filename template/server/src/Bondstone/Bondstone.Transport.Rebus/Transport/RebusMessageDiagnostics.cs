using System.Diagnostics;
using Bondstone.Messaging;

namespace Bondstone.Transport.Rebus;

internal static class RebusMessageDiagnostics
{
    public static Activity? StartHandlingActivity<TMessage>(
        string moduleName,
        string messageId,
        string messageIdentity,
        IReadOnlyDictionary<string, string> headers)
    {
        Activity? activity = BondstoneDiagnostics.StartConsumerActivity(
            $"Bondstone handle {typeof(TMessage).Name}",
            GetHeader(headers, BondstoneDiagnostics.TraceParentHeader),
            GetHeader(headers, BondstoneDiagnostics.TraceStateHeader));
        if (activity is null)
        {
            return null;
        }

        activity.SetTag("messaging.system", "rebus");
        activity.SetTag("messaging.operation", "process");
        activity.SetTag("messaging.message.id", messageId);
        activity.SetTag("messaging.message.type", messageIdentity);
        activity.SetTag("bondstone.module", moduleName);

        foreach (KeyValuePair<string, string?> baggage in BondstoneDiagnostics.ExtractBaggage(headers))
        {
            if (!string.IsNullOrWhiteSpace(baggage.Value))
            {
                activity.AddBaggage(baggage.Key, baggage.Value);
            }
        }

        AddGuidBaggageAndTag(
            activity,
            BondstoneDiagnostics.CausationIdBaggageKey,
            ParseGuidOrNull(messageId));
        AddGuidBaggageAndTag(
            activity,
            BondstoneDiagnostics.OperationIdBaggageKey,
            GetOperationId(headers));

        return activity;
    }

    public static void AddTraceHeaders(
        Dictionary<string, string> headers,
        string? metadata)
    {
        MessageTraceContext? traceContext = MessageTraceContext.FromMetadata(metadata);
        if (traceContext is null)
        {
            return;
        }

        AddIfPresent(headers, BondstoneDiagnostics.TraceParentHeader, traceContext.TraceParent);
        AddIfPresent(headers, BondstoneDiagnostics.TraceStateHeader, traceContext.TraceState);
        AddIfPresent(headers, BondstoneDiagnostics.BaggageHeader, traceContext.Baggage);
    }

    private static Guid? GetOperationId(IReadOnlyDictionary<string, string> headers)
    {
        return headers.TryGetValue(BondstoneMessageHeaders.OperationId, out string? operationId)
            ? ParseGuidOrNull(operationId)
            : null;
    }

    private static Guid? ParseGuidOrNull(string? value)
    {
        return Guid.TryParse(value, out Guid parsedValue)
            ? parsedValue
            : null;
    }

    private static void AddGuidBaggageAndTag(
        Activity activity,
        string key,
        Guid? value)
    {
        if (value is null)
        {
            return;
        }

        string formattedValue = value.Value.ToString("D");
        activity.AddBaggage(key, formattedValue);
        activity.SetTag(key, formattedValue);
    }

    private static string? GetHeader(
        IReadOnlyDictionary<string, string> headers,
        string name)
    {
        return headers.TryGetValue(name, out string? value)
            ? value
            : null;
    }

    private static void AddIfPresent(Dictionary<string, string> headers, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            headers[key] = value;
        }
    }
}
