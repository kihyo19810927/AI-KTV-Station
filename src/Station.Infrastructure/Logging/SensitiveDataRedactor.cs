namespace Station.Infrastructure.Logging;

public static class SensitiveDataRedactor
{
    private static readonly string[] SensitiveFragments = ["password", "secret", "token", "cookie", "authorization", "path"];

    public static object? Redact(string key, object? value) =>
        SensitiveFragments.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            ? "[REDACTED]"
            : value;

    public static IReadOnlyDictionary<string, object?> Redact(IReadOnlyDictionary<string, object?> properties) =>
        properties.ToDictionary(pair => pair.Key, pair => Redact(pair.Key, pair.Value), StringComparer.OrdinalIgnoreCase);
}
