using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;

namespace Launcher.Helpers;

public static class ProtonHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static string GetConfiguredPath(string fileName)
    {
        var file = Path.Combine(AppContext.BaseDirectory, fileName);
        if (!File.Exists(file))
            return string.Empty;

        try
        {
            var value = File.ReadAllText(file).Trim();
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
        }
        catch (IOException ex)
        {
            Logger.Warn(ex, "Could not read configured Proton/Steam path file {file}", file);
            return string.Empty;
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.Warn(ex, "Could not access configured Proton/Steam path file {file}", file);
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

        var candidates = new List<string>();

        foreach (var library in SteamLibraries())
        {
            var common = Path.Combine(library, "steamapps", "common");
            if (!Directory.Exists(common))
                continue;

            try
            {
                foreach (var directory in Directory.EnumerateDirectories(common, "Proton*"))
                {
                    var candidate = Path.Combine(directory, "proton");
                    if (File.Exists(candidate))
                        candidates.Add(candidate);
                }
            }
            catch (IOException ex)
            {
                Logger.Warn(ex, "Could not enumerate Proton installations in {path}", common);
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Warn(ex, "Could not access Proton installations in {path}", common);
            }
        }

        foreach (var root in SteamRoots())
        {
            var tools = Path.Combine(root, "compatibilitytools.d");
            if (!Directory.Exists(tools))
                continue;

            try
            {
                candidates.AddRange(Directory.EnumerateFiles(tools, "proton", SearchOption.AllDirectories));
            }
            catch (IOException ex)
            {
                Logger.Warn(ex, "Could not enumerate compatibility tools in {path}", tools);
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Warn(ex, "Could not access compatibility tools in {path}", tools);
            }
        }

        return candidates
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(IsExperimental)
            .ThenByDescending(IsGeProton)
            .ThenByDescending(p => GetProtonVersion(p).Major)
            .ThenByDescending(p => GetProtonVersion(p).Minor)
            .ThenByDescending(p => GetProtonVersion(p).Patch)
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault() ?? string.Empty;
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
                var text = File.ReadAllText(vdf);
                foreach (Match match in Regex.Matches(text, "\\\"path\\\"\\s+\\\"(?<path>[^\\\"]+)\\\"", RegexOptions.IgnoreCase))
                {
                    var path = match.Groups["path"].Value.Replace("\\\\", "\\");
                    if (Directory.Exists(path))
                        libraries.Add(path);
                }
            }
            catch (IOException ex)
            {
                Logger.Warn(ex, "Could not read Steam library file {file}", vdf);
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Warn(ex, "Could not access Steam library file {file}", vdf);
            }
        }

        return libraries;
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
