using System.Text.RegularExpressions;

namespace OSFR.Linux.Installer.Services;

public static class HostInfoParser
{
    public static string? ParseOsRelease(string text)
    {
        var values = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1].Trim().Trim('"'), StringComparer.OrdinalIgnoreCase);

        if (values.TryGetValue("PRETTY_NAME", out var prettyName) && !string.IsNullOrWhiteSpace(prettyName))
            return prettyName;

        if (values.TryGetValue("NAME", out var name) && !string.IsNullOrWhiteSpace(name))
        {
            values.TryGetValue("VERSION_ID", out var version);
            return string.IsNullOrWhiteSpace(version) ? name : $"{name} {version}";
        }

        return null;
    }

    public static string? ParseCpuInfo(string text)
    {
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!line.StartsWith("model name", StringComparison.OrdinalIgnoreCase) &&
                !line.StartsWith("Hardware", StringComparison.OrdinalIgnoreCase))
                continue;

            var separator = line.IndexOf(':');
            if (separator < 0)
                continue;

            var model = line[(separator + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(model))
                return model;
        }

        return null;
    }

    public static string? ParseMemory(string text)
    {
        var line = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(value => value.StartsWith("MemTotal:", StringComparison.OrdinalIgnoreCase));
        if (line is null)
            return null;

        var match = Regex.Match(line, @"MemTotal:\s+(?<kb>\d+)\s+kB", RegexOptions.IgnoreCase);
        if (!match.Success || !long.TryParse(match.Groups["kb"].Value, out var kb))
            return null;

        return $"{kb / 1024d / 1024d:0.#} GiB";
    }

    public static IReadOnlyList<string> ParseLspciGraphics(string text)
    {
        return text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Contains("VGA compatible controller", StringComparison.OrdinalIgnoreCase) ||
                           line.Contains("3D controller", StringComparison.OrdinalIgnoreCase) ||
                           line.Contains("Display controller", StringComparison.OrdinalIgnoreCase))
            .Select(line =>
            {
                var index = line.IndexOf(": ", StringComparison.Ordinal);
                return index >= 0 ? line[(index + 2)..].Trim() : line.Trim();
            })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
