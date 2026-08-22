using OSFR.Linux.Installer.Services;
using Xunit;

namespace Installer.UnitTests;

public sealed class HostInfoParserTests
{
    [Fact]
    public void ParseOsRelease_PrefersPrettyName()
    {
        const string text = """
            NAME="Fedora Linux"
            VERSION_ID="44"
            PRETTY_NAME="Fedora Linux 44"
            """;

        Assert.Equal("Fedora Linux 44", HostInfoParser.ParseOsRelease(text));
    }

    [Fact]
    public void ParseOsRelease_FallsBackToNameAndVersion()
    {
        const string text = """
            NAME="Debian GNU/Linux"
            VERSION_ID="13"
            """;

        Assert.Equal("Debian GNU/Linux 13", HostInfoParser.ParseOsRelease(text));
    }

    [Fact]
    public void ParseCpuInfo_ReadsX86ModelName()
    {
        const string text = """
            processor : 0
            model name : Intel(R) Core(TM) i7-7700 CPU @ 3.60GHz
            """;

        Assert.Equal("Intel(R) Core(TM) i7-7700 CPU @ 3.60GHz", HostInfoParser.ParseCpuInfo(text));
    }

    [Fact]
    public void ParseCpuInfo_ReadsArmHardwareFallback()
    {
        const string text = """
            Processor : AArch64 Processor rev 4
            Hardware : Example ARM Board
            """;

        Assert.Equal("Example ARM Board", HostInfoParser.ParseCpuInfo(text));
    }

    [Fact]
    public void ParseMemory_ConvertsKiBToGiB()
    {
        const string text = "MemTotal:       33554432 kB\nMemFree: 1 kB\n";
        Assert.Equal("32 GiB", HostInfoParser.ParseMemory(text));
    }

    [Fact]
    public void ParseLspciGraphics_FindsMultipleAdapters()
    {
        const string text = """
            00:02.0 VGA compatible controller: Intel Corporation HD Graphics 630
            01:00.0 VGA compatible controller: NVIDIA Corporation TU102 [GeForce RTX 2080 Ti]
            02:00.0 Ethernet controller: Intel Corporation Ethernet
            """;

        var adapters = HostInfoParser.ParseLspciGraphics(text);
        Assert.Equal(2, adapters.Count);
        Assert.Contains("Intel Corporation HD Graphics 630", adapters);
        Assert.Contains("NVIDIA Corporation TU102 [GeForce RTX 2080 Ti]", adapters);
    }
}
