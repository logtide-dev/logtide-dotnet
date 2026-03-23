namespace LogTide.SDK.Breadcrumbs;

public sealed class Breadcrumb
{
    public string Type { get; set; } = "custom";
    public string Message { get; set; } = string.Empty;
    public string? Level { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object?> Data { get; set; } = new();
}
