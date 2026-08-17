using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace OSFR.Linux.Installer.Services;

public sealed record ProtonCandidate(string Name, string Path, bool Recommended = false);

public sealed record SystemState(
    bool IsLinux,
    bool IsX64,
    string? SteamRoot,
    string? ProtonPath,
    IReadOnlyList<ProtonCandidate>? ProtonCandidates = null)
{
    public bool Ready => IsLinux && IsX64 && SteamRoot is not null && ProtonPath is not null;

    public SystemState WithProton(string protonPath) => this with { ProtonPath = protonPath };
}

public static class SystemDetector
{
    public static SystemState Detect()
    {
        var isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        var isX64 = RuntimeInformation.OSArchitecture == Architecture.X64;
        var libraries = FindSteamLibraries().Select(CanonicalizePath).Distinct(StringComparer.Ordinal).ToList();
        var steamRoot = FindSteamRoots().FirstOrDefault(Directory.Exists);
        var candidates = FindProtonCandidates(libraries);
        var proton = candidates.FirstOrDefault()?.Path;

        InstallerLog.Info($"System detection: Linux={isLinux}, x64={isX64}, Steam={(steamRoot ?? "not found")}, Proton={(proton ?? "not found")}, ProtonCandidates={candidates.Count}");
        return new SystemState(isLinux, isX64, steamRoot, proton, candidates);
    }

    public static IEnumerable<string> FindSteamRoots()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Path.Combine(home, ".steam", "debian-installation");
        yield return Path.Combine(home, ".local", "share", "Steam");
        yield return Path.Combine(home, ".steam", "steam");
        yield return Path.Combine(home, ".steam", "root");
        yield return Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", "data", "Steam");
        yield return Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam");
    }

    public static IEnumerable<string> FindSteamLibraries()
    {
        foreach (var root in FindSteamRoots().Where(Directory.Exists))
        {
            yield return root;

            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf))
                continue;

            string text;
            try
            {
                text = File.ReadAllText(vdf);
            }
            catch (IOException ex)
            {
                InstallerLog.Warn($"Could not read Steam library file {vdf}: {ex.Message}");
                continue;
            }
            catch (UnauthorizedAccessException ex)
            {
                InstallerLog.Warn($"Could not access Steam library file {vdf}: {ex.Message}");
                continue;
            }

            foreach (var path in ParseSteamLibraryPaths(text))
            {
                if (Directory.Exists(path))
                    yield return path;
            }
        }
    }

    public static IEnumerable<string> ParseSteamLibraryPaths(string vdfText)
    {
        foreach (Match match in Regex.Matches(vdfText, "\\\"path\\\"\\s+\\\"(?<path>[^\\\"]+)\\\"", RegexOptions.IgnoreCase))
        {
            var path = match.Groups["path"].Value.Replace("\\\\", "\\");
            if (!string.IsNullOrWhiteSpace(path))
                yield return path;
        }
    }

    public static IReadOnlyList<ProtonCandidate> FindProtonCandidates(IEnumerable<string>? libraries = null)
    {
        var candidates = new List<string>();
        var libraryList = (libraries ?? FindSteamLibraries())
            .Concat(FindSteamRoots())
            .Where(Directory.Exists)
            .Select(CanonicalizePath)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var library in libraryList)
        {
            AddSteamCommonCandidates(library, candidates);
            AddCompatibilityToolCandidates(Path.Combine(library, "compatibilitytools.d"), candidates);
        }

        var envToolPaths = Environment.GetEnvironmentVariable("STEAM_COMPAT_TOOL_PATHS");
        if (!string.IsNullOrWhiteSpace(envToolPaths))
        {
            foreach (var path in envToolPaths.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                AddCompatibilityToolCandidates(path, candidates);
        }

        var ordered = candidates
            .Where(File.Exists)
            .Select(CanonicalizePath)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(IsExperimental)
            .ThenByDescending(IsGeProton)
            .ThenByDescending(p => GetProtonVersion(p).Major)
            .ThenByDescending(p => GetProtonVersion(p).Minor)
            .ThenByDescending(p => GetProtonVersion(p).Patch)
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return ordered.Select((path, index) => new ProtonCandidate(GetProtonDirectoryName(path), path, index == 0)).ToList();
    }

    public static string CanonicalizePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        try
        {
            var info = Directory.Exists(fullPath)
                ? (FileSystemInfo)new DirectoryInfo(fullPath)
                : new FileInfo(fullPath);
            var target = info.ResolveLinkTarget(returnFinalTarget: true);
            return target is null ? fullPath : Path.GetFullPath(target.FullName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            InstallerLog.Warn($"Could not resolve filesystem alias {fullPath}: {ex.Message}");
            return fullPath;
        }
    }

    private static void AddSteamCommonCandidates(string library, ICollection<string> candidates)
    {
        var common = Path.Combine(library, "steamapps", "common");
        if (!Directory.Exists(common))
            return;

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(common))
            {
                var proton = Path.Combine(directory, "proton");
                if (File.Exists(proton))
                    candidates.Add(proton);
            }
        }
        catch (IOException ex)
        {
            InstallerLog.Warn($"Could not enumerate Proton builds in {common}: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            InstallerLog.Warn($"Could not access Proton builds in {common}: {ex.Message}");
        }
    }

    private static void AddCompatibilityToolCandidates(string tools, ICollection<string> candidates)
    {
        if (!Directory.Exists(tools))
            return;

        try
        {
            var directProton = Path.Combine(tools, "proton");
            if (File.Exists(directProton))
                candidates.Add(directProton);

            foreach (var directory in Directory.EnumerateDirectories(tools))
            {
                var proton = Path.Combine(directory, "proton");
                if (File.Exists(proton))
                    candidates.Add(proton);
            }
        }
        catch (IOException ex)
        {
            InstallerLog.Warn($"Could not enumerate compatibility tools in {tools}: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            InstallerLog.Warn($"Could not access compatibility tools in {tools}: {ex.Message}");
        }
    }

    private static bool IsExperimental(string protonPath) =>
        GetProtonDirectoryName(protonPath).Contains("Experimental", StringComparison.OrdinalIgnoreCase);

    private static bool IsGeProton(string protonPath) =>
        GetProtonDirectoryName(protonPath).Contains("GE-Proton", StringComparison.OrdinalIgnoreCase);

    private static (int Major, int Minor, int Patch) GetProtonVersion(string protonPath)
    {
        var name = GetProtonDirectoryName(protonPath);
        var match = Regex.Match(name, @"(?:GE-Proton|Proton\s*-?\s*)(?<major>\d+)(?:[.-](?<minor>\d+))?(?:[.-](?<patch>\d+))?", RegexOptions.IgnoreCase);
        if (!match.Success)
            return (0, 0, 0);

        _ = int.TryParse(match.Groups["major"].Value, out var major);
        _ = int.TryParse(match.Groups["minor"].Value, out var minor);
        _ = int.TryParse(match.Groups["patch"].Value, out var patch);
        return (major, minor, patch);
    }

    private static string GetProtonDirectoryName(string protonPath) =>
        Path.GetFileName(Path.GetDirectoryName(protonPath)) ?? string.Empty;
}
