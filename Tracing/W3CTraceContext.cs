using System.Security.Cryptography;

namespace LogTide.SDK.Tracing;

public static class W3CTraceContext
{
    public const string HeaderName = "traceparent";

    public static (string TraceId, string SpanId)? Parse(string? traceparent)
    {
        if (string.IsNullOrEmpty(traceparent)) return null;
        var parts = traceparent.Split('-');
        if (parts.Length != 4 || parts[0] != "00") return null;
        if (parts[1].Length != 32 || parts[2].Length != 16) return null;
        if (!IsLowercaseHex(parts[1]) || !IsLowercaseHex(parts[2])) return null;
        if (parts[3].Length != 2 || !IsLowercaseHex(parts[3])) return null;
        // W3C spec: all-zeros traceId and parentId are invalid
        if (parts[1] == "00000000000000000000000000000000") return null;
        if (parts[2] == "0000000000000000") return null;
        return (parts[1], parts[2]);
    }

    private static bool IsLowercaseHex(string s)
    {
        foreach (var c in s)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
        }
        return true;
    }

    public static string Create(string traceId, string spanId, bool sampled = true) =>
        $"00-{traceId}-{spanId}-{(sampled ? "01" : "00")}";

    public static string GenerateTraceId() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    public static string GenerateSpanId() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
}
