using System.Runtime.InteropServices;
using CodeTitans.Odysseus;

namespace UnitTests;

public sealed class DeviceInfoTests
{
    [Fact]
    public void capture_device_info_reports_current_os_platform()
    {
        var client = new OdysseusClient("1", "1");
        var info = client.CaptureDeviceInfo();

        Assert.True(info.TryGetValue("platform", out var platform));

        var expected = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".NET Core (Windows)"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? ".NET Core (Linux)"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? ".NET Core (macOS)"
            : null;

        Assert.NotNull(expected);
        Assert.Equal(expected, platform);
    }

    [Fact]
    public void capture_app_info_reports_entry_assembly_name()
    {
        var client = new OdysseusClient("1", "1");
        var info = client.CaptureAppInfo();

        Assert.True(info.ContainsKey("app_name"));
        Assert.True(info.ContainsKey("debug"));
    }

    [Fact]
    public void capture_info_merges_and_overrides_with_extra()
    {
        var client = new OdysseusClient("1", "1");
        var info = client.CaptureDeviceInfo(new Dictionary<string, object> { ["platform"] = "custom", ["extra_key"] = 42 });

        Assert.Equal("custom", info["platform"]);
        Assert.Equal(42, info["extra_key"]);
    }
}
