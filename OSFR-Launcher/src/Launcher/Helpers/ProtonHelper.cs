using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Launcher.Helpers;

public static class ProtonHelper
{
    public static string GetConfiguredPath(string fileName)
    {
        try
        {
            var file = Path.Combine(AppContext.BaseDirectory, fileName);
            if (!File.Exists(file))
                return string.Empty;

            var value = File.ReadAllText(file).Trim();
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string GetSteamRoot()
    {
        var configured = GetConfiguredPath("steam-path.txt");
        if (Directory.Exists(configured))
            return configured;

        return SteamRoots().FirstOrDefault() ?? string.Empty;
    }

    public static string GetPath()
    {
        var configured = GetConfiguredPath("proton-path.txt");
        if (File.Exists(configured))
            return configured;

        var preferred = new[] { "Proton - Experimental", "Proton Hotfix" };

        foreach (var library in SteamLibraries())
        {
            var common = Path.Combine(library, "steamapps", "common");
            if (!Directory.Exists(common))
                continue;

            foreach (var name in preferred)
            {
                var candidate = Path.Combine(common, name, "proton");
                if (File.Exists(candidate))
                    return candidate;
            }

            try
            {
                foreach (var directory in Directory.EnumerateDirectories(common))
                {
                    var name = Path.GetFileName(directory);
                    var candidate = Path.Combine(directory, "proton");
                    if (name.Contains("Proton", StringComparison.OrdinalIgnoreCase) && File.Exists(candidate))
                        return candidate;
                }
            }
            catch { }
        }

        foreach (var root in SteamRoots())
        {
            var tools = Path.Combine(root, "compatibilitytools.d");
            if (!Directory.Exists(tools))
                continue;

            try
            {
                foreach (var candidate in Directory.EnumerateFiles(tools, "proton", SearchOption.AllDirectories))
                    return candidate;
            }
            catch { }
        }

        return string.Empty;
    }

    private static IEnumerable<string> SteamRoots()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string[] roots =
        [
            Path.Combine(home, ".steam/debian-installation"),
            Path.Combine(home, ".local/share/Steam"),
            Path.Combine(home, ".steam/steam"),
            Path.Combine(home, ".var/app/com.valvesoftware.Steam/data/Steam"),
            Path.Combine(home, ".var/app/com.valvesoftware.Steam/.local/share/Steam")
        ];

        return roots.Where(Directory.Exists).Distinct(StringComparer.Ordinal);
    }

    private static IEnumerable<string> SteamLibraries()
    {
        var libraries = new HashSet<string>(StringComparer.Ordinal);

        foreach (var root in SteamRoots())
        {
            libraries.Add(root);

            var vdf = Path.Combine(root, "steamapps/libraryfolders.vdf");
            if (!File.Exists(vdf))
                continue;

            try
            {
                foreach (var line in File.ReadLines(vdf))
                {
                    var match = Regex.Match(line, "\\\"path\\\"\\s+\\\"([^\\\"]+)\\\"");
                    if (!match.Success)
                        continue;

                    var path = match.Groups[1].Value.Replace("\\\\", "\\");
                    if (Directory.Exists(path))
                        libraries.Add(path);
                }
            }
            catch { }
        }

        return libraries;
    }
}
