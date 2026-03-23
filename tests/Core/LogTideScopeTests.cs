using Xunit;
using LogTide.SDK.Breadcrumbs;
using LogTide.SDK.Core;

namespace LogTide.SDK.Tests.Core;

public class LogTideScopeTests
{
    [Fact]
    public void Create_SetsCurrentScope()
    {
        using var scope = LogTideScope.Create("abc123");
        Assert.Equal("abc123", LogTideScope.Current?.TraceId);
    }

    [Fact]
    public void Dispose_RestoresPreviousScope()
    {
        using var outer = LogTideScope.Create("outer");
        using (var inner = LogTideScope.Create("inner"))
        {
            Assert.Equal("inner", LogTideScope.Current?.TraceId);
        }
        Assert.Equal("outer", LogTideScope.Current?.TraceId);
    }

    [Fact]
    public void Create_WithNullTraceId_GeneratesId()
    {
        using var scope = LogTideScope.Create();
        Assert.NotNull(scope.TraceId);
        Assert.Equal(32, scope.TraceId.Length);
    }

    [Fact]
    public async Task AsyncLocal_IsolatesAcrossAsyncContexts()
    {
        string? traceInTask = null;
        using var scope = LogTideScope.Create("main-trace");

        await Task.Run(() =>
        {
            using var inner = LogTideScope.Create("task-trace");
            traceInTask = LogTideScope.Current?.TraceId;
        });

        // After task finishes, main context unchanged
        Assert.Equal("main-trace", LogTideScope.Current?.TraceId);
        Assert.Equal("task-trace", traceInTask);
    }

    [Fact]
    public void AddBreadcrumb_StoredInScope()
    {
        using var scope = LogTideScope.Create("t");
        scope.AddBreadcrumb(new Breadcrumb { Message = "click" });
        Assert.Single(scope.GetBreadcrumbs());
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var scope = LogTideScope.Create("t");
        scope.Dispose();
        scope.Dispose(); // should not throw or corrupt state
    }

    [Fact]
    public async Task ConcurrentRequests_HaveIsolatedScopes()
    {
        string? trace1 = null, trace2 = null;
        var t1 = Task.Run(() => { using var s = LogTideScope.Create("req-1"); trace1 = LogTideScope.Current?.TraceId; });
        var t2 = Task.Run(() => { using var s = LogTideScope.Create("req-2"); trace2 = LogTideScope.Current?.TraceId; });
        await Task.WhenAll(t1, t2);
        Assert.Equal("req-1", trace1);
        Assert.Equal("req-2", trace2);
    }
}
