namespace Bondstone.Internal;

public static class StringExtensions
{
    public static bool ContainsTrimmedOrdinal(this IEnumerable<string>? values, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return values?.Any(item => string.Equals(item?.Trim(), value, StringComparison.Ordinal)) == true;
    }

    public static string[] TrimDistinctRequired(this IEnumerable<string>? values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);

        string[] normalized = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return normalized.Length == 0
            ? throw new ArgumentException("At least one non-empty value is required.", parameterName)
            : normalized;
    }

    public static string? TrimToNull(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    public static string TrimRequired(this string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

        string trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Value must not be empty or whitespace.", parameterName);
        }

        return trimmed;
    }
}
