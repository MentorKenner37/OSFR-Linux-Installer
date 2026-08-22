using System.ComponentModel;
using System.Diagnostics;

namespace OSFR.Linux.Installer.Services;

public enum ProbeState
{
    Available,
    Missing,
    Unknown
}

public sealed record CompatibilitySnapshot(
    string SteamInstallType,
    ProbeState FreeType32,
    ProbeState OpenGl32,
    ProbeState Vulkan64,
    ProbeState Vulkan32,
    string RecommendedGraphicsBackend,
    string GraphicsRecommendationReason,
    IReadOnlyList<string> Warnings,
    string? PackageGuidance)
{
    public bool HasKnownRuntimeProblem => FreeType32 == ProbeState.Missing || OpenGl32 == ProbeState.Missing;
}

public static class CompatibilityAdvisor
{
    public static CompatibilitySnapshot Detect(SystemState state)
    {
        var libraries = ReadLdConfig();
        var freeType32 = ProbeLibrary(libraries, "libfreetype.so.6", elfClass: 1);
        var openGl32 = ProbeAnyLibrary(libraries, ["libGL.so.1", "libOpenGL.so.0"], elfClass: 1);
        var vulkan64 = ProbeLibrary(libraries, "libvulkan.so.1", elfClass: 2);
        var vulkan32 = ProbeLibrary(libraries, "libvulkan.so.1", elfClass: 1);

        var warnings = new List<string>();
        if (state.Desktop.Contains("cinnamon", StringComparison.OrdinalIgnoreCase) &&
            state.SessionType.Equals("wayland", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("Cinnamon + Wayland detected. Shift/modifier input issues have been observed in one tested Free Realms configuration. If Shift-walk fails, try Cinnamon/X11 or another tested desktop session.");
        }

        if (freeType32 == ProbeState.Missing)
            warnings.Add("32-bit FreeType was not detected. The 32-bit Free Realms client may fail to start.");
        if (openGl32 == ProbeState.Missing)
            warnings.Add("32-bit OpenGL userspace was not detected. Proton/WineD3D and parts of the 32-bit graphics stack may fail to start.");

        var recommendedBackend = GraphicsBackendConfig.Dxvk;
        var reason = "DXVK/Vulkan is the preferred graphics path when the 32-bit Vulkan loader is available.";
        if (vulkan32 == ProbeState.Missing)
        {
            recommendedBackend = GraphicsBackendConfig.WineD3D;
            reason = "32-bit Vulkan support was not detected, so WineD3D/OpenGL is the safer default.";
        }
        else if (vulkan32 == ProbeState.Unknown)
        {
            reason = "32-bit Vulkan availability could not be verified. DXVK remains the default, but WineD3D/OpenGL is available if Vulkan fails.";
        }

        return new CompatibilitySnapshot(
            DetectSteamInstallType(state.SteamRoot),
            freeType32,
            openGl32,
            vulkan64,
            vulkan32,
            recommendedBackend,
            reason,
            warnings,
            BuildPackageGuidance(state.OsName, freeType32, openGl32, vulkan32));
    }

    public static string DetectSteamInstallType(string? steamRoot)
    {
        if (string.IsNullOrWhiteSpace(steamRoot))
            return "Not detected";

        return steamRoot.Contains("/.var/app/com.valvesoftware.Steam/", StringComparison.OrdinalIgnoreCase)
            ? "Flatpak Steam"
            : "Native Steam";
    }

    public static string ProbeLabel(ProbeState state) => state switch
    {
        ProbeState.Available => "available",
        ProbeState.Missing => "missing",
        _ => "unknown"
    };

    private static string? BuildPackageGuidance(string osName, ProbeState freeType32, ProbeState openGl32, ProbeState vulkan32)
    {
        if (freeType32 != ProbeState.Missing && openGl32 != ProbeState.Missing && vulkan32 != ProbeState.Missing)
            return null;

        if (osName.Contains("Debian", StringComparison.OrdinalIgnoreCase) ||
            osName.Contains("Ubuntu", StringComparison.OrdinalIgnoreCase) ||
            osName.Contains("Mint", StringComparison.OrdinalIgnoreCase))
        {
            return "Debian/Ubuntu/Mint: enable i386 and install libfreetype6:i386 libgl1:i386 libgl1-mesa-dri:i386 libglx-mesa0:i386. For Vulkan, install the matching 32-bit Vulkan loader/driver package for your GPU.";
        }

        if (osName.Contains("Fedora", StringComparison.OrdinalIgnoreCase))
            return "Fedora: install the matching i686 FreeType/Mesa/Vulkan userspace packages for your GPU (for example freetype.i686 and vulkan-loader.i686).";

        if (osName.Contains("Arch", StringComparison.OrdinalIgnoreCase))
            return "Arch: enable multilib and install lib32-freetype2, lib32-mesa, lib32-vulkan-icd-loader, plus the matching 32-bit Vulkan driver for your GPU.";

        if (osName.Contains("openSUSE", StringComparison.OrdinalIgnoreCase) || osName.Contains("SUSE", StringComparison.OrdinalIgnoreCase))
            return "openSUSE: install the matching 32-bit FreeType, Mesa/OpenGL and Vulkan loader/driver packages for your GPU.";

        return "Install the distribution's 32-bit FreeType, OpenGL/Mesa and Vulkan loader/driver packages required by 32-bit Proton applications.";
    }

    private static Dictionary<string, List<string>>? ReadLdConfig()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "ldconfig",
                Arguments = "-p",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null)
                return null;

            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(2000) || process.ExitCode != 0)
                return null;

            var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var arrow = line.IndexOf("=>", StringComparison.Ordinal);
                if (arrow < 0)
                    continue;
                var left = line[..arrow].Trim();
                var name = left.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                var path = line[(arrow + 2)..].Trim();
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
                    continue;
                if (!result.TryGetValue(name, out var paths))
                    result[name] = paths = [];
                paths.Add(path);
            }
            return result;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            InstallerLog.Warn($"Could not query ldconfig for compatibility diagnostics: {ex.Message}");
            return null;
        }
    }

    private static ProbeState ProbeAnyLibrary(Dictionary<string, List<string>>? libraries, string[] names, byte elfClass)
    {
        if (libraries is null)
            return ProbeState.Unknown;
        foreach (var name in names)
        {
            if (ProbeLibrary(libraries, name, elfClass) == ProbeState.Available)
                return ProbeState.Available;
        }
        return ProbeState.Missing;
    }

    private static ProbeState ProbeLibrary(Dictionary<string, List<string>>? libraries, string name, byte elfClass)
    {
        if (libraries is null)
            return ProbeState.Unknown;
        if (!libraries.TryGetValue(name, out var paths))
            return ProbeState.Missing;

        foreach (var path in paths.Distinct(StringComparer.Ordinal))
        {
            if (TryReadElfClass(path) == elfClass)
                return ProbeState.Available;
        }
        return ProbeState.Missing;
    }

    private static byte? TryReadElfClass(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[5];
            if (stream.Read(header) < 5 || header[0] != 0x7f || header[1] != (byte)'E' || header[2] != (byte)'L' || header[3] != (byte)'F')
                return null;
            return header[4];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            InstallerLog.Warn($"Could not inspect runtime library {path}: {ex.Message}");
            return null;
        }
    }
}
