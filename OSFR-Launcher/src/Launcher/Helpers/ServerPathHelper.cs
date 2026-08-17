using System;
using System.IO;

namespace Launcher.Helpers;

public static class ServerPathHelper
{
    public static string ServersRoot => Path.GetFullPath(
        Path.Combine(Constants.SavePath, Constants.ServersDirectory));

    public static string GetServerDirectory(string savePath)
    {
        if (string.IsNullOrWhiteSpace(savePath))
            throw new InvalidDataException("The server save path is missing.");

        var root = ServersRoot;
        var candidate = Path.GetFullPath(
            Path.IsPathRooted(savePath)
                ? savePath
                : Path.Combine(root, savePath));

        if (!IsWithin(candidate, root, allowRoot: false))
            throw new InvalidDataException("The server save path is outside the OSFR Servers directory.");

        return candidate;
    }

    public static string GetClientDirectory(string savePath, string relativePath = "")
    {
        var clientRoot = Path.GetFullPath(Path.Combine(GetServerDirectory(savePath), "Client"));
        var candidate = string.IsNullOrEmpty(relativePath)
            ? clientRoot
            : Path.GetFullPath(Path.Combine(clientRoot, relativePath));

        if (!IsWithin(candidate, clientRoot, allowRoot: true))
            throw new InvalidDataException("The client manifest contains an unsafe folder path.");

        return candidate;
    }

    public static string GetClientFilePath(string savePath, string relativePath, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new InvalidDataException("The client manifest contains an empty file name.");

        var clientRoot = GetClientDirectory(savePath);
        var directory = GetClientDirectory(savePath, relativePath);
        var candidate = Path.GetFullPath(Path.Combine(directory, fileName));

        if (!IsWithin(candidate, clientRoot, allowRoot: false))
            throw new InvalidDataException("The client manifest contains an unsafe file path.");

        return candidate;
    }

    private static bool IsWithin(string candidate, string root, bool allowRoot)
    {
        candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

        if (allowRoot && string.Equals(candidate, root, StringComparison.Ordinal))
            return true;

        var prefix = root + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.Ordinal);
    }
}
