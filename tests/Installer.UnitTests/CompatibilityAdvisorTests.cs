using System;
using System.IO;
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

    [Theory]
    [InlineData("Arch Linux", true)]
    [InlineData("CachyOS", true)]
    [InlineData("EndeavourOS", true)]
    [InlineData("Manjaro Linux", true)]
    [InlineData("Garuda Linux", true)]
    [InlineData("Debian GNU/Linux 13", false)]
    [InlineData("Fedora Linux 44", false)]
    public void ArchFamilyDetection_RecognizesDerivatives(string osName, bool expected)
    {
        Assert.Equal(expected, CompatibilityAdvisor.IsArchFamily(osName));
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
    public void RequiredRuntimeReady_IsFalseWhenFreeTypeOrOpenGlIsMissing()
    {
        var missingFreeType = Snapshot(ProbeState.Missing, ProbeState.Available, ProbeState.Available);
        var missingOpenGl = Snapshot(ProbeState.Available, ProbeState.Missing, ProbeState.Available);

        Assert.False(missingFreeType.RequiredRuntimeReady);
        Assert.False(missingOpenGl.RequiredRuntimeReady);
        Assert.True(missingFreeType.HasKnownRuntimeProblem);
        Assert.True(missingOpenGl.HasKnownRuntimeProblem);
    }

    [Fact]
    public void RequiredRuntimeReady_DoesNotRequireVulkan()
    {
        var snapshot = Snapshot(ProbeState.Available, ProbeState.Available, ProbeState.Missing);
        Assert.True(snapshot.RequiredRuntimeReady);
        Assert.False(snapshot.HasKnownRuntimeProblem);
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

    [Fact]
    public void ParseLdConfigOutput_GroupsMultipleArchitecturesByLibraryName()
    {
        const string output = """
            2 libs found in cache `/etc/ld.so.cache'
            libvulkan.so.1 (libc6,x86-64) => /lib64/libvulkan.so.1
            libvulkan.so.1 (libc6) => /lib32/libvulkan.so.1
            """;

        var parsed = CompatibilityAdvisor.ParseLdConfigOutput(output);
        Assert.True(parsed.TryGetValue("libvulkan.so.1", out var paths));
        Assert.NotNull(paths);
        Assert.Equal(2, paths!.Count);
        Assert.Contains("/lib64/libvulkan.so.1", paths);
        Assert.Contains("/lib32/libvulkan.so.1", paths);
    }

    [Fact]
    public void ProbeLibraryPaths_DistinguishesElf32AndElf64()
    {
        var root = Path.Combine(Path.GetTempPath(), $"osfr-elf-probe-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var elf32 = Path.Combine(root, "lib32.so");
        var elf64 = Path.Combine(root, "lib64.so");

        try
        {
            File.WriteAllBytes(elf32, ElfHeader(1));
            File.WriteAllBytes(elf64, ElfHeader(2));

            Assert.Equal(ProbeState.Available, CompatibilityAdvisor.ProbeLibraryPaths([elf32, elf64], 1));
            Assert.Equal(ProbeState.Available, CompatibilityAdvisor.ProbeLibraryPaths([elf32, elf64], 2));
            Assert.Equal(ProbeState.Missing, CompatibilityAdvisor.ProbeLibraryPaths([elf64], 1));
            Assert.Equal(ProbeState.Unknown, CompatibilityAdvisor.ProbeLibraryPaths(null, 1));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static CompatibilitySnapshot Snapshot(ProbeState freeType, ProbeState openGl, ProbeState vulkan32) =>
        new("Native Steam", freeType, openGl, ProbeState.Available, vulkan32, GraphicsBackendConfig.Dxvk, "test", [], null);

    private static byte[] ElfHeader(byte elfClass) =>
    [
        0x7f, (byte)'E', (byte)'L', (byte)'F', elfClass,
        1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0
    ];

    private static ProtonCandidate Candidate(string name, bool compatible) =>
        new(name, $"/tmp/{name}/proton", "/tmp/steam", "x86_64", compatible, "test");
}
