using LogTide.SDK.Core;

namespace LogTide.SDK.Integrations;

public interface IIntegration
{
    string Name { get; }
    void Setup(ILogTideClient client);
    void Teardown();
}
