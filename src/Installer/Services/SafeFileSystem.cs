namespace OSFR.Linux.Installer.Services;

internal static class SafeFileSystem
{
    public static bool IsPathInside(string path, string parent)
    {
        var fullPath = Path.GetFullPath(path);
        var fullParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(fullParent, StringComparison.Ordinal);
    }

    public static bool IsSymbolicLink(string path)
    {
        try
        {
            FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
            return info.Exists && (info.LinkTarget is not null || info.Attributes.HasFlag(FileAttributes.ReparsePoint));
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool HasSymbolicLinkAncestor(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var current = Directory.Exists(fullPath)
            ? new DirectoryInfo(fullPath)
            : new DirectoryInfo(Path.GetDirectoryName(fullPath) ?? fullPath);

        while (current is not null)
        {
            if (current.Exists && IsSymbolicLink(current.FullName))
                return true;
            current = current.Parent;
        }

        return false;
    }

    public static void RefuseSymbolicLink(string path, string description)
    {
        if (IsSymbolicLink(path))
            throw new InvalidOperationException($"Refusing to use a symbolic link as the {description}: {path}");
    }

    public static void RefuseSymbolicLinkAncestors(string path, string description)
    {
        if (HasSymbolicLinkAncestor(path))
            throw new InvalidOperationException($"Refusing to use the {description} because an existing path component is a symbolic link: {path}");
    }

    public static bool IsSafeArchiveEntry(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName) ||
            Path.IsPathRooted(entryName) ||
            entryName.StartsWith("/", StringComparison.Ordinal) ||
            entryName.StartsWith("\\", StringComparison.Ordinal))
            return false;

        var normalized = entryName.Replace('\\', '/');
        return normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .All(segment => segment is not "." and not "..");
    }

    public static void EnsureExecutable(string path, string description)
    {
        if (!OperatingSystem.IsLinux())
            return;

        try
        {
            var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                       UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                       UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
            File.SetUnixFileMode(path, mode);
            var actual = File.GetUnixFileMode(path);
            if ((actual & UnixFileMode.UserExecute) == 0)
                throw new IOException("The execute permission was not applied.");
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException($"The installer could not make the {description} executable. Check permissions for {path}.", ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"The installer could not make the {description} executable. The filesystem may not support Unix execute permissions; use a local Linux filesystem or adjust its mount options. Path: {path}", ex);
        }
    }

    public static void DeleteDirectoryTreeNoFollow(string path)
    {
        if (!Directory.Exists(path) && !IsSymbolicLink(path))
            return;

        var rootInfo = new DirectoryInfo(path);
        if (rootInfo.LinkTarget is not null || rootInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            rootInfo.Delete(false);
            return;
        }

        foreach (var entry in rootInfo.EnumerateFileSystemInfos())
        {
            if (entry.LinkTarget is not null || entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                if (entry is DirectoryInfo linkDirectory)
                    linkDirectory.Delete(false);
                else
                    entry.Delete();
                continue;
            }

            if (entry is DirectoryInfo directory)
                DeleteDirectoryTreeNoFollow(directory.FullName);
            else
                entry.Delete();
        }

        rootInfo.Delete(false);
    }
}