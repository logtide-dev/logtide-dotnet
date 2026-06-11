using Xunit;
using LogTide.SDK.Core;
using LogTide.SDK.Models;
using LogTide.SDK.Tests.Helpers;

namespace LogTide.SDK.Tests.Core;

/// <summary>
/// Exceptions must be serialized to the platform's canonical StructuredException
/// contract (metadata.exception with type/message/language, structured frames and
/// a nested cause chain) so server-side error grouping and fingerprinting work.
/// </summary>
public class ExceptionSerializationTests
{
    private static (LogTideClient client, FakeTransport transport) Create()
    {
        var opts = new ClientOptions
        {
            ApiUrl = "http://localhost:8080",
            ApiKey = "lp_test_key",
            FlushIntervalMs = 60000,
            Debug = false
        };
        var fake = new FakeTransport();
        var client = new LogTideClient(opts, fake, fake);
        return (client, fake);
    }

    private static Exception Thrown()
    {
        try
        {
            throw new InvalidOperationException("db timeout", new ApplicationException("root cause"));
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    [Fact]
    public async Task Error_SerializesExceptionUnderCanonicalKey()
    {
        var (client, fake) = Create();
        client.Error("svc", "boom", Thrown());
        await client.FlushAsync();

        var metadata = fake.LogBatches[0][0].Metadata;
        Assert.True(metadata.ContainsKey("exception"), "metadata.exception missing");
        Assert.False(metadata.ContainsKey("error"), "legacy metadata.error key must not be used");

        var exception = Assert.IsType<Dictionary<string, object?>>(metadata["exception"]);
        Assert.Equal("System.InvalidOperationException", exception["type"]);
        Assert.Equal("db timeout", exception["message"]);
        Assert.Equal("csharp", exception["language"]);
        Assert.NotNull(exception["raw"]);
    }

    [Fact]
    public async Task Error_SerializesCauseChain()
    {
        var (client, fake) = Create();
        client.Error("svc", "boom", Thrown());
        await client.FlushAsync();

        var exception = Assert.IsType<Dictionary<string, object?>>(fake.LogBatches[0][0].Metadata["exception"]);
        var cause = Assert.IsType<Dictionary<string, object?>>(exception["cause"]);
        Assert.Equal("root cause", cause["message"]);
        Assert.Equal("System.ApplicationException", cause["type"]);
    }

    [Fact]
    public async Task Error_IncludesStructuredFrames()
    {
        var (client, fake) = Create();
        client.Error("svc", "boom", Thrown());
        await client.FlushAsync();

        var exception = Assert.IsType<Dictionary<string, object?>>(fake.LogBatches[0][0].Metadata["exception"]);
        var frames = Assert.IsType<List<Dictionary<string, object?>>>(exception["stacktrace"]);
        Assert.NotEmpty(frames);
        Assert.True(frames[0].ContainsKey("function"));
        Assert.Contains("Thrown", frames[0]["function"]!.ToString());
    }

    [Fact]
    public async Task Critical_UsesCanonicalKeyToo()
    {
        var (client, fake) = Create();
        client.Critical("svc", "boom", Thrown());
        await client.FlushAsync();

        Assert.True(fake.LogBatches[0][0].Metadata.ContainsKey("exception"));
    }
}

public class SdkMetadataTests
{
    private static (LogTideClient client, FakeTransport transport) Create()
    {
        var opts = new ClientOptions
        {
            ApiUrl = "http://localhost:8080",
            ApiKey = "lp_test_key",
            FlushIntervalMs = 60000,
            Debug = false
        };
        var fake = new FakeTransport();
        var client = new LogTideClient(opts, fake, fake);
        return (client, fake);
    }

    [Fact]
    public async Task Entries_CarrySdkMetadata()
    {
        var (client, fake) = Create();
        client.Info("svc", "hello");
        await client.FlushAsync();

        var metadata = fake.LogBatches[0][0].Metadata;
        var sdk = Assert.IsType<Dictionary<string, object?>>(metadata["sdk"]);
        Assert.Equal("logtide-dotnet", sdk["name"]);
        Assert.False(string.IsNullOrEmpty(sdk["version"]?.ToString()));
    }

    [Fact]
    public async Task CallerProvidedSdkMetadataWins()
    {
        var (client, fake) = Create();
        client.Info("svc", "hello", new Dictionary<string, object?> { ["sdk"] = "custom" });
        await client.FlushAsync();

        Assert.Equal("custom", fake.LogBatches[0][0].Metadata["sdk"]);
    }
}
