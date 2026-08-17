using System;
using System.IO;
using OSFR.Linux.Installer.Services;
using Xunit;

namespace Installer.UnitTests;

public sealed class GraphicsBackendConfigTests : IDisposable
{
    private readonly TempDirFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(GraphicsBackendConfig.Dxvk)]
    [InlineData(GraphicsBackendConfig.WineD3D)]
    public void Write_persists_selected_backend(string backend)
    {
        var launcherDir = Path.Combine(_fixture.Root, "Launcher");
        Directory.CreateDirectory(launcherDir);
        File.WriteAllText(Path.Combine(launcherDir, "OSFRLauncher"), "stub");

        GraphicsBackendConfig.Write(_fixture.Root, backend);

        var config = Path.Combine(launcherDir, GraphicsBackendConfig.FileName);
        Assert.True(File.Exists(config));
        Assert.Equal(backend, File.ReadAllText(config).Trim());
    }

    [Fact]
    public void Write_rejects_unknown_backend()
    {
        var launcherDir = Path.Combine(_fixture.Root, "Launcher");
        Directory.CreateDirectory(launcherDir);
        File.WriteAllText(Path.Combine(launcherDir, "OSFRLauncher"), "stub");

        Assert.Throws<ArgumentOutOfRangeException>(() => GraphicsBackendConfig.Write(_fixture.Root, "mystery"));
    }
}
