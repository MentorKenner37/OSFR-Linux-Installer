using System;
using System.IO;
using System.Linq;

namespace Launcher.Extensions;

public static class DirectoryExtensions
{
    public static string ToValidDirectoryName(this string name)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var chars = name
            .Trim()
            .Select(c => invalid.Contains(c) || c is '/' or '\\' || char.IsControl(c) ? '_' : c)
            .ToArray();

        var result = new string(chars).Trim();
        if (string.IsNullOrWhiteSpace(result) || result is "." or "..")
            return "Server";

        return result;
    }
}
