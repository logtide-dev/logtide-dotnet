using Xunit;
using NSubstitute;
using LogTide.SDK.Core;
using LogTide.SDK.Integrations;

namespace LogTide.SDK.Tests.Integrations;

public class GlobalErrorIntegrationTests
{
    [Fact]
    public void Setup_RegistersHandlers_TeardownUnregisters()
    {
        var client = Substitute.For<ILogTideClient>();
        var integration = new GlobalErrorIntegration();
        integration.Setup(client);
        Assert.Equal("GlobalError", integration.Name);
        integration.Teardown(); // should not throw
    }

    [Fact]
    public void OnUnobservedTaskException_CallsClientError()
    {
        var client = Substitute.For<ILogTideClient>();
        var integration = new GlobalErrorIntegration();
        integration.Setup(client);

        var ex = new AggregateException(new InvalidOperationException("oops"));
        integration.SimulateUnobservedTaskException(ex);

        client.Received(1).Error(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Exception>());
        integration.Teardown();
    }
}
