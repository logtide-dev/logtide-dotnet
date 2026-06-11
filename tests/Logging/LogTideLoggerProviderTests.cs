using Microsoft.Extensions.Logging;
using Xunit;
using LogTide.SDK.Core;
using LogTide.SDK.Logging;
using LogTide.SDK.Models;
using LogTide.SDK.Tests.Helpers;

namespace LogTide.SDK.Tests.Logging;

public class LogTideLoggerProviderTests
{
    private static (LogTideClient client, FakeTransport transport) CreateClient()
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

    private static (ILogger logger, LogTideClient client, FakeTransport transport) Create(
        string category = "MyApp.Controller",
        Action<LogTideLoggerOptions>? configure = null)
    {
        var (client, fake) = CreateClient();
        var options = new LogTideLoggerOptions { Service = "logger-test" };
        configure?.Invoke(options);
        var provider = new LogTideLoggerProvider(client, options);
        return (provider.CreateLogger(category), client, fake);
    }

    [Fact]
    public async Task Log_RoutesThroughClient()
    {
        var (logger, client, fake) = Create();

        logger.LogInformation("User {UserId} logged in", 42);
        await client.FlushAsync();

        var entry = Assert.Single(Assert.Single(fake.LogBatches));
        Assert.Equal(SDK.Enums.LogLevel.Info, entry.Level);
        Assert.Equal("User 42 logged in", entry.Message);
        Assert.Equal("logger-test", entry.Service);
        Assert.Equal(42, entry.Metadata["UserId"]);
        Assert.Equal("MyApp.Controller", entry.Metadata["logger"]);
    }

    [Theory]
    [InlineData(LogLevel.Trace, SDK.Enums.LogLevel.Debug)]
    [InlineData(LogLevel.Debug, SDK.Enums.LogLevel.Debug)]
    [InlineData(LogLevel.Information, SDK.Enums.LogLevel.Info)]
    [InlineData(LogLevel.Warning, SDK.Enums.LogLevel.Warn)]
    [InlineData(LogLevel.Error, SDK.Enums.LogLevel.Error)]
    [InlineData(LogLevel.Critical, SDK.Enums.LogLevel.Critical)]
    public async Task Log_MapsLevels(LogLevel msLevel, SDK.Enums.LogLevel expected)
    {
        var (logger, client, fake) = Create(configure: o => o.MinimumLevel = LogLevel.Trace);

        logger.Log(msLevel, "msg");
        await client.FlushAsync();

        Assert.Equal(expected, fake.LogBatches[0][0].Level);
    }

    [Fact]
    public void IsEnabled_RespectsMinimumLevel()
    {
        var (logger, _, _) = Create(configure: o => o.MinimumLevel = LogLevel.Warning);

        Assert.False(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Warning));
        Assert.False(logger.IsEnabled(LogLevel.None));
    }

    [Fact]
    public async Task Log_BelowMinimumIsDropped()
    {
        var (logger, client, fake) = Create(configure: o => o.MinimumLevel = LogLevel.Warning);

        logger.LogInformation("nope");
        await client.FlushAsync();

        Assert.Empty(fake.LogBatches);
    }

    [Fact]
    public async Task Log_AttachesCanonicalException()
    {
        var (logger, client, fake) = Create();

        logger.LogError(new InvalidOperationException("kaboom"), "operation failed");
        await client.FlushAsync();

        var entry = fake.LogBatches[0][0];
        Assert.Equal("operation failed", entry.Message);
        var exception = Assert.IsType<Dictionary<string, object?>>(entry.Metadata["exception"]);
        Assert.Equal("kaboom", exception["message"]);
        Assert.Equal("csharp", exception["language"]);
    }

    [Fact]
    public async Task Log_IncludesScopeValues()
    {
        var (logger, client, fake) = Create();

        using (logger.BeginScope(new Dictionary<string, object?> { ["tenant"] = "acme" }))
        {
            logger.LogInformation("scoped");
        }
        await client.FlushAsync();

        Assert.Equal("acme", fake.LogBatches[0][0].Metadata["tenant"]);
    }

    [Fact]
    public async Task AddLogTide_RegistersProviderOnBuilder()
    {
        var (client, fake) = CreateClient();
        using var factory = LoggerFactory.Create(builder =>
            builder.AddLogTide(client, o => o.Service = "factory-test"));

        factory.CreateLogger("Cat").LogWarning("via factory");
        await client.FlushAsync();

        var entry = Assert.Single(Assert.Single(fake.LogBatches));
        Assert.Equal(SDK.Enums.LogLevel.Warn, entry.Level);
        Assert.Equal("factory-test", entry.Service);
    }
}
