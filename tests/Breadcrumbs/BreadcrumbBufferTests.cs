using Xunit;
using LogTide.SDK.Breadcrumbs;

namespace LogTide.SDK.Tests.Breadcrumbs;

public class BreadcrumbBufferTests
{
    [Fact]
    public void Add_StoresItems()
    {
        var buf = new BreadcrumbBuffer(maxSize: 5);
        buf.Add(new Breadcrumb { Message = "hello" });
        Assert.Single(buf.GetAll());
    }

    [Fact]
    public void Add_EvictsOldestWhenFull()
    {
        var buf = new BreadcrumbBuffer(maxSize: 3);
        buf.Add(new Breadcrumb { Message = "1" });
        buf.Add(new Breadcrumb { Message = "2" });
        buf.Add(new Breadcrumb { Message = "3" });
        buf.Add(new Breadcrumb { Message = "4" }); // should evict "1"
        var all = buf.GetAll();
        Assert.Equal(3, all.Count);
        Assert.Equal("2", all[0].Message);
        Assert.Equal("4", all[2].Message);
    }

    [Fact]
    public void GetAll_ReturnsSnapshot()
    {
        var buf = new BreadcrumbBuffer(2);
        buf.Add(new Breadcrumb { Message = "a" });
        var snap1 = buf.GetAll();
        buf.Add(new Breadcrumb { Message = "b" });
        Assert.Single(snap1); // snapshot unchanged
    }
}
