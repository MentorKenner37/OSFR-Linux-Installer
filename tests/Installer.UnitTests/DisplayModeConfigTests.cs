using System;
using System.IO;
using OSFR.Linux.Installer.Services;
using Xunit;

namespace Installer.UnitTests;

public sealed class DisplayModeConfigTests : IDisposable
{
    private readonly TempDirFixture _fixture = new();

    [Theory]
    [InlineData(DisplayModeConfig.Fullscreen, 1920, 1080)]
    [InlineData(DisplayModeConfig.Windowed, 1280, 720)]
    public void WriteAndReadRoundTrip(string mode, int width, int height)
    {
        var installRoot = CreateInstalledLauncher();
        DisplayModeConfig.Write(installRoot, mode, width, height);
        Assert.Equal(new DisplayModeSettings(mode, width, height), DisplayModeConfig.Read(installRoot));
    }

    [Fact]
    public void MissingConfigurationDefaultsToFullscreen()
    {
        var settings = DisplayModeConfig.Read(Path.Combine(_fixture.Root, "missing"), 2560, 1440);
        Assert.Equal(new DisplayModeSettings(DisplayModeConfig.Fullscreen, 2560, 1440), settings);
    }

    [Fact]
    public void InvalidModeIsRejected()
    {
        var installRoot = CreateInstalledLauncher();
        Assert.Throws<ArgumentOutOfRangeException>(() => DisplayModeConfig.Write(installRoot, "borderless-ish", 1280, 720));
    }

    private string CreateInstalledLauncher()
    {
        var installRoot = Path.Combine(_fixture.Root, "Sanctuary");
        var launcherDir = Path.Combine(installRoot, "Launcher");
        Directory.CreateDirectory(launcherDir);
        File.WriteAllText(Path.Combine(launcherDir, "OSFRLauncher"), "stub");
        return installRoot;
    }

    public void Dispose() => _fixture.Dispose();
}
