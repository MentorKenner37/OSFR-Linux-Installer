namespace OSFR.Linux.Installer.Services;

public static class InstallerLog
{
    private static readonly object Gate = new();

    public static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".local", "share", "OSFR-Linux", "installer.log");

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? exception = null)
    {
        var detail = exception is null ? message : $"{message}: {exception.GetType().Name}: {exception.Message}";
        Write("ERROR", detail);
    }

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                var directory = Path.GetDirectoryName(LogPath)!;
                Directory.CreateDirectory(directory);
                File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}");

                if (OperatingSystem.IsLinux())
                {
                    File.SetUnixFileMode(
                        LogPath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
            }
        }
        catch (IOException)
        {
            // Logging must never make installation itself fail.
        }
        catch (UnauthorizedAccessException)
        {
            // Logging must never make installation itself fail.
        }
    }
}
