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
        Assert.True(recommendation.Reason.Contains("could not be verified", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PreferredProton_ChoosesNewestStableBeforeExperimentalAndGe()
    {
        var candidates = new[]
        {
            Candidate("Proton Experimental", true),
            Candidate("GE-Proton11-5", true),
            Candidate("Proton 9.0", true),
            Candidate("Proton 10.0", true),
            Candidate("Proton 11.0", true)
        };

        var preferred = CompatibilityAdvisor.SelectPreferredProton(candidates);
        Assert.NotNull(preferred);
        Assert.Equal("Proton 11.0", preferred!.Name);
    }

    [Fact]
    public void PreferredProton_FallsBackToGeWhenNoStableExists()
    {
        var candidates = new[]
        {
            Candidate("Proton Experimental", true),
            Candidate("GE-Proton10-30", true),
            Candidate("GE-Proton11-5", true)
        };

        var preferred = CompatibilityAdvisor.SelectPreferredProton(candidates);
        Assert.NotNull(preferred);
        Assert.Equal("GE-Proton11-5", preferred!.Name);
    }

    [Fact]
    public void PreferredProton_IgnoresIncompatibleBuilds()
    {
        var candidates = new[]
        {
            Candidate("Proton 12.0", false),
            Candidate("Proton 11.0", true)
        };

        var preferred = CompatibilityAdvisor.SelectPreferredProton(candidates);
        Assert.NotNull(preferred);
        Assert.Equal("Proton 11.0", preferred!.Name);
    }

    private static ProtonCandidate Candidate(string name, bool compatible) =>
        new(name, $"/tmp/{name}/proton", "/tmp/steam", "x86_64", compatible, "test");
}
