namespace OSFR.Linux.Installer.Services;

public static class InstallerLog
{
    private static readonly object Gate = new();
    private const long MaxLogBytes = 1_000_000;
    private const int MaxRotatedLogs = 3;

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
                RotateIfNeeded();
                File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}");

                if (OperatingSystem.IsLinux())
                {
                    File.SetUnixFileMode(
                        LogPath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
            }
        }
        catch (IOException ex)
        {
            TryWriteFallbackDiagnostic($"Installer logging failed: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            TryWriteFallbackDiagnostic($"Installer logging permission failure: {ex.Message}");
        }
    }

    private static void RotateIfNeeded()
    {
        if (!File.Exists(LogPath) || new FileInfo(LogPath).Length < MaxLogBytes)
            return;

        var oldest = $"{LogPath}.{MaxRotatedLogs}";
        if (File.Exists(oldest))
            File.Delete(oldest);

        for (var index = MaxRotatedLogs - 1; index >= 1; index--)
        {
            var source = $"{LogPath}.{index}";
            var destination = $"{LogPath}.{index + 1}";
            if (File.Exists(source))
                File.Move(source, destination, overwrite: true);
        }

        File.Move(LogPath, $"{LogPath}.1", overwrite: true);
    }

    private static void TryWriteFallbackDiagnostic(string message)
    {
        try
        {
            Console.Error.WriteLine(message);
        }
        catch
        {
            // There is nowhere else safe to report a logging failure.
        }
    }
}
