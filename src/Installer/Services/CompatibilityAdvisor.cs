using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

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
    public bool RequiredRuntimeReady => FreeType32 != ProbeState.Missing && OpenGl32 != ProbeState.Missing;
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
        if (NeedsCinnamonWaylandWarning(state.Desktop, state.SessionType))
        {
            warnings.Add("Cinnamon + Wayland detected. Shift/modifier input issues have been observed in one tested Free Realms configuration. If Shift-walk fails, try Cinnamon/X11 or another tested desktop session.");
        }

        if (freeType32 == ProbeState.Missing)
            warnings.Add("REQUIRED: 32-bit FreeType was not detected. The 32-bit Free Realms client may fail to start.");
        if (openGl32 == ProbeState.Missing)
            warnings.Add("REQUIRED: 32-bit OpenGL userspace was not detected. Proton/WineD3D and parts of the 32-bit graphics stack may fail to start.");
        if (vulkan32 == ProbeState.Missing)
            warnings.Add("OPTIONAL: 32-bit Vulkan was not detected. DXVK is unavailable, but WineD3D/OpenGL can still be used.");

        var (recommendedBackend, reason) = RecommendGraphicsBackend(vulkan32);

        return new CompatibilitySnapshot(
            DetectSteamInstallType(state.SteamRoot),
            freeType32,
            openGl32,
            vulkan64,
            vulkan32,
            recommendedBackend,
            reason,
            warnings,
            BuildPackageGuidance(state.OsName, state.Gpu, freeType32, openGl32, vulkan32));
    }

    public static ProtonCandidate? SelectPreferredProton(IEnumerable<ProtonCandidate> candidates)
    {
        var compatible = candidates.Where(candidate => candidate.Compatible).ToList();
        if (compatible.Count == 0)
            return null;

        static (int Major, int Minor, int Patch) VersionOf(ProtonCandidate candidate)
        {
            var match = Regex.Match(candidate.Name, @"(?:GE-Proton|Proton\s*-?\s*)(?<major>\d+)(?:[.-](?<minor>\d+))?(?:[.-](?<patch>\d+))?", RegexOptions.IgnoreCase);
            if (!match.Success)
                return (0, 0, 0);
            _ = int.TryParse(match.Groups["major"].Value, out var major);
            _ = int.TryParse(match.Groups["minor"].Value, out var minor);
            _ = int.TryParse(match.Groups["patch"].Value, out var patch);
            return (major, minor, patch);
        }

        static IOrderedEnumerable<ProtonCandidate> ByNewest(IEnumerable<ProtonCandidate> source) => source
            .OrderByDescending(candidate => VersionOf(candidate).Major)
            .ThenByDescending(candidate => VersionOf(candidate).Minor)
            .ThenByDescending(candidate => VersionOf(candidate).Patch)
            .ThenBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase);

        var stable = ByNewest(compatible.Where(candidate =>
            candidate.Name.StartsWith("Proton ", StringComparison.OrdinalIgnoreCase) &&
            !candidate.Name.Contains("Experimental", StringComparison.OrdinalIgnoreCase) &&
            !candidate.Name.Contains("GE-Proton", StringComparison.OrdinalIgnoreCase))).FirstOrDefault();
        if (stable is not null)
            return stable;

        var ge = ByNewest(compatible.Where(candidate => candidate.Name.Contains("GE-Proton", StringComparison.OrdinalIgnoreCase))).FirstOrDefault();
        if (ge is not null)
            return ge;

        var experimental = compatible.FirstOrDefault(candidate => candidate.Name.Contains("Experimental", StringComparison.OrdinalIgnoreCase));
        return experimental ?? compatible.First();
    }

    public static bool NeedsCinnamonWaylandWarning(string desktop, string sessionType) =>
        desktop.Contains("cinnamon", StringComparison.OrdinalIgnoreCase) &&
        sessionType.Equals("wayland", StringComparison.OrdinalIgnoreCase);

    public static bool IsArchFamily(string osName) =>
        osName.Contains("Arch", StringComparison.OrdinalIgnoreCase) ||
        osName.Contains("CachyOS", StringComparison.OrdinalIgnoreCase) ||
        osName.Contains("EndeavourOS", StringComparison.OrdinalIgnoreCase) ||
        osName.Contains("Manjaro", StringComparison.OrdinalIgnoreCase) ||
        osName.Contains("Garuda", StringComparison.OrdinalIgnoreCase);

    public static (string Backend, string Reason) RecommendGraphicsBackend(ProbeState vulkan32) => vulkan32 switch
    {
        ProbeState.Available => (GraphicsBackendConfig.Dxvk, "DXVK/Vulkan is the preferred graphics path because the 32-bit Vulkan loader is available."),
        ProbeState.Missing => (GraphicsBackendConfig.WineD3D, "32-bit Vulkan support was not detected, so WineD3D/OpenGL is the safer default."),
        _ => (GraphicsBackendConfig.Dxvk, "32-bit Vulkan availability could not be verified. DXVK remains the default, but WineD3D/OpenGL is available if Vulkan fails.")
    };

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

    public static Dictionary<string, List<string>> ParseLdConfigOutput(string output)
    {
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

    public static ProbeState ProbeLibraryPaths(IEnumerable<string>? paths, byte elfClass)
    {
        if (paths is null)
            return ProbeState.Unknown;

        var any = false;
        foreach (var path in paths.Distinct(StringComparer.Ordinal))
        {
            any = true;
            if (TryReadElfClass(path) == elfClass)
                return ProbeState.Available;
        }
        return any ? ProbeState.Missing : ProbeState.Missing;
    }

    private static string? BuildPackageGuidance(string osName, string gpu, ProbeState freeType32, ProbeState openGl32, ProbeState vulkan32)
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

        if (IsArchFamily(osName))
        {
            var driver = gpu.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)
                ? "lib32-nvidia-utils"
                : gpu.Contains("AMD", StringComparison.OrdinalIgnoreCase) || gpu.Contains("Radeon", StringComparison.OrdinalIgnoreCase)
                    ? "lib32-vulkan-radeon"
                    : gpu.Contains("Intel", StringComparison.OrdinalIgnoreCase)
                        ? "lib32-vulkan-intel"
                        : "the matching 32-bit Vulkan driver for your GPU (lib32-nvidia-utils, lib32-vulkan-radeon, or lib32-vulkan-intel)";
            return $"Arch/CachyOS family: enable multilib and install lib32-freetype2 lib32-mesa lib32-vulkan-icd-loader plus {driver}. 32-bit FreeType/OpenGL are required; 32-bit Vulkan is preferred for DXVK but WineD3D/OpenGL remains available without it.";
        }

        if (osName.Contains("openSUSE", StringComparison.OrdinalIgnoreCase) || osName.Contains("SUSE", StringComparison.OrdinalIgnoreCase))
            return "openSUSE: install the matching 32-bit FreeType, Mesa/OpenGL and Vulkan loader/driver packages for your GPU.";

        return "Install the distribution's required 32-bit FreeType and OpenGL/Mesa packages. Install the matching 32-bit Vulkan loader/driver for DXVK; Vulkan is preferred but not mandatory because WineD3D/OpenGL is available as a fallback.";
    }

    private static Dictionary<string, List<string>>? ReadLdConfig()
    {
        foreach (var executable in new[] { "ldconfig", "/usr/sbin/ldconfig", "/sbin/ldconfig" })
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = "-p",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = "/"
                });

                if (process is null)
                    continue;

                var output = process.StandardOutput.ReadToEnd();
                if (!process.WaitForExit(2000) || process.ExitCode != 0)
                    continue;

                return ParseLdConfigOutput(output);
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                InstallerLog.Warn($"Could not query {executable} for compatibility diagnostics: {ex.Message}");
            }
        }

        return null;
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
        return ProbeLibraryPaths(paths, elfClass);
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
