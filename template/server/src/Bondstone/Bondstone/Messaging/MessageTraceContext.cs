using System.Diagnostics;
using System.Text.Json;

namespace Bondstone.Messaging;

public sealed class MessageTraceContext
{
    public string? TraceParent { get; init; }

    public string? TraceState { get; init; }

    public string? Baggage { get; init; }

    public static string? CaptureMetadata()
    {
        Activity? activity = Activity.Current;
        if (activity?.Id is null)
        {
            return null;
        }

        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        BondstoneDiagnostics.InjectTraceHeaders(activity, headers);
        var traceContext = new MessageTraceContext
        {
            TraceParent = GetHeader(headers, BondstoneDiagnostics.TraceParentHeader),
            TraceState = GetHeader(headers, BondstoneDiagnostics.TraceStateHeader),
            Baggage = GetHeader(headers, BondstoneDiagnostics.BaggageHeader)
        };

        return JsonSerializer.Serialize(traceContext);
    }

    public static MessageTraceContext? FromMetadata(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<MessageTraceContext>(metadata);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetHeader(IReadOnlyDictionary<string, string> headers, string name)
    {
        return headers.TryGetValue(name, out string? value)
            ? value
            : null;
    }
}
