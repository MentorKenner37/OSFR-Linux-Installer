using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Launcher.Helpers;

public static class WineHelper
{
    private static IEnumerable<string> SteamRoots()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string[] roots =
        [
            Path.Combine(home, ".steam/debian-installation"),
            Path.Combine(home, ".local/share/Steam"),
            Path.Combine(home, ".steam/steam"),
            Path.Combine(home, ".var/app/com.valvesoftware.Steam/.local/share/Steam")
        ];

        return roots.Where(Directory.Exists).Distinct();
    }

    private static IEnumerable<string> SteamLibraries()
    {
        var libraries = new HashSet<string>();

        foreach (string root in SteamRoots())
        {
            libraries.Add(root);

            string vdf = Path.Combine(root, "steamapps/libraryfolders.vdf");
            if (!File.Exists(vdf))
                continue;

            try
            {
                foreach (string line in File.ReadLines(vdf))
                {
                    Match match = Regex.Match(line, "\\\"path\\\"\\s+\\\"([^\\\"]+)\\\"");
                    if (!match.Success)
                        continue;

                    string path = match.Groups[1].Value.Replace("\\\\", "\\");
                    if (Directory.Exists(path))
                        libraries.Add(path);
                }
            }
            catch
            {
                // Ignore an unreadable Steam library file and continue scanning.
            }
        }

        return libraries;
    }

    public static string GetPath()
    {
        var preferred = new[] { "Proton - Experimental", "Proton Hotfix" };

        foreach (string library in SteamLibraries())
        {
            string common = Path.Combine(library, "steamapps", "common");
            if (!Directory.Exists(common))
                continue;

            foreach (string name in preferred)
            {
                string candidate = Path.Combine(common, name, "proton");
                if (File.Exists(candidate))
                    return candidate;
            }

            try
            {
                foreach (string directory in Directory.EnumerateDirectories(common))
                {
                    string name = Path.GetFileName(directory);
                    string candidate = Path.Combine(directory, "proton");

                    if (name.Contains("Proton", StringComparison.OrdinalIgnoreCase) && File.Exists(candidate))
                        return candidate;
                }
            }
            catch
            {
                // Continue to the next Steam library.
            }
        }

        return string.Empty;
    }

    public static bool IsInstalled()
    {
        return !string.IsNullOrEmpty(GetPath());
    }
}
