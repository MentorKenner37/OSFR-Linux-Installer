using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace OSFR.Linux.Installer.Services;

public sealed record SystemState(
    bool IsLinux,
    bool IsX64,
    string? SteamRoot,
    string? ProtonPath)
{
    public bool Ready => IsLinux && IsX64 && SteamRoot is not null && ProtonPath is not null;
}

public static class SystemDetector
{
    public static SystemState Detect()
    {
        var isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        var isX64 = RuntimeInformation.OSArchitecture == Architecture.X64;
        var libraries = FindSteamLibraries().Distinct(StringComparer.Ordinal).ToList();
        var steamRoot = FindSteamRoots().FirstOrDefault(Directory.Exists);
        var proton = FindProton(libraries);

        return new SystemState(isLinux, isX64, steamRoot, proton);
    }

    public static IEnumerable<string> FindSteamRoots()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        yield return Path.Combine(home, ".steam", "debian-installation");
        yield return Path.Combine(home, ".local", "share", "Steam");
        yield return Path.Combine(home, ".steam", "steam");
        yield return Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", "data", "Steam");
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
            try { text = File.ReadAllText(vdf); }
            catch { continue; }

            foreach (Match match in Regex.Matches(text, "\\\"path\\\"\\s+\\\"(?<path>[^\\\"]+)\\\""))
            {
                var path = match.Groups["path"].Value.Replace("\\\\", "\\");
                if (Directory.Exists(path))
                    yield return path;
            }
        }
    }

    private static string? FindProton(IEnumerable<string> libraries)
    {
        var candidates = new List<string>();

        foreach (var library in libraries)
        {
            var common = Path.Combine(library, "steamapps", "common");
            if (!Directory.Exists(common))
                continue;

            try
            {
                foreach (var directory in Directory.EnumerateDirectories(common, "Proton*"))
                {
                    var proton = Path.Combine(directory, "proton");
                    if (File.Exists(proton))
                        candidates.Add(proton);
                }
            }
            catch { }
        }

        foreach (var root in FindSteamRoots().Where(Directory.Exists))
        {
            var tools = Path.Combine(root, "compatibilitytools.d");
            if (!Directory.Exists(tools))
                continue;

            try
            {
                foreach (var proton in Directory.EnumerateFiles(tools, "proton", SearchOption.AllDirectories))
                    candidates.Add(proton);
            }
            catch { }
        }

        return candidates
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(p => p.Contains("Proton - Experimental", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(p => p.Contains("GE-Proton", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(p => p, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }
}
