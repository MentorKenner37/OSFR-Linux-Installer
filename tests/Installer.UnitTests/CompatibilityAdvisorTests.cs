using OSFR.Linux.Installer.Services;
using Xunit;

namespace Installer.UnitTests;

public sealed class CompatibilityAdvisorTests
{
    [Fact]
    public void DetectSteamInstallType_RecognizesFlatpak()
    {
        var kind = CompatibilityAdvisor.DetectSteamInstallType("/home/test/.var/app/com.valvesoftware.Steam/data/Steam");
        Assert.Equal("Flatpak Steam", kind);
    }

    [Fact]
    public void DetectSteamInstallType_RecognizesNativeSteam()
    {
        var kind = CompatibilityAdvisor.DetectSteamInstallType("/home/test/.local/share/Steam");
        Assert.Equal("Native Steam", kind);
    }

    [Theory]
    [InlineData("X-Cinnamon", "wayland", true)]
    [InlineData("Cinnamon", "Wayland", true)]
    [InlineData("Cinnamon", "x11", false)]
    [InlineData("GNOME", "wayland", false)]
    public void CinnamonWaylandWarning_IsSpecific(string desktop, string session, bool expected)
    {
        Assert.Equal(expected, CompatibilityAdvisor.NeedsCinnamonWaylandWarning(desktop, session));
    }

    [Fact]
    public void Missing32BitVulkan_RecommendsWineD3D()
    {
        var recommendation = CompatibilityAdvisor.RecommendGraphicsBackend(ProbeState.Missing);
        Assert.Equal(GraphicsBackendConfig.WineD3D, recommendation.Backend);
    }

    [Fact]
    public void Available32BitVulkan_RecommendsDxvk()
    {
        var recommendation = CompatibilityAdvisor.RecommendGraphicsBackend(ProbeState.Available);
        Assert.Equal(GraphicsBackendConfig.Dxvk, recommendation.Backend);
    }

    [Fact]
    public void Unknown32BitVulkan_KeepsDxvkAsNonBlockingDefault()
    {
        var recommendation = CompatibilityAdvisor.RecommendGraphicsBackend(ProbeState.Unknown);
        Assert.Equal(GraphicsBackendConfig.Dxvk, recommendation.Backend);
        Assert.Contains("could not be verified", recommendation.Reason, StringComparison.OrdinalIgnoreCase);
    }
}
