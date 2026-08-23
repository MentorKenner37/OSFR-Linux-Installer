namespace OSFR.Linux.Installer.Services;

internal sealed record DisplayModeSettings(string Mode, int Width, int Height);

internal static class DisplayModeConfig
{
    public const string Fullscreen = "fullscreen";
    public const string Windowed = "windowed";
    public const string FileName = "display-mode.txt";

    public static string DisplayName(string mode) => mode == Windowed
        ? "Boxed window (1280 × 720)"
        : "Fullscreen (desktop resolution)";

    public static DisplayModeSettings Read(string installRoot, int fallbackWidth = 1920, int fallbackHeight = 1080)
    {
        var path = Path.Combine(InstallService.NormalizeInstallRoot(installRoot), "Launcher", FileName);
        if (!File.Exists(path))
            return new(Fullscreen, fallbackWidth, fallbackHeight);

        var parts = File.ReadAllText(path).Trim().Split(':');
        if (parts.Length == 3 && IsValidMode(parts[0]) &&
            int.TryParse(parts[1], out var width) && int.TryParse(parts[2], out var height) &&
            IsValidResolution(width, height))
            return new(parts[0], width, height);

        return new(Fullscreen, fallbackWidth, fallbackHeight);
    }

    public static void Write(string installRoot, string mode, int width, int height)
    {
        if (!IsValidMode(mode))
            throw new ArgumentOutOfRangeException(nameof(mode), "Unknown display mode.");
        if (!IsValidResolution(width, height))
            throw new ArgumentOutOfRangeException(nameof(width), "Display resolution is outside the supported range.");

        var launcherDir = Path.Combine(InstallService.NormalizeInstallRoot(installRoot), "Launcher");
        var launcher = Path.Combine(launcherDir, "OSFRLauncher");
        if (!File.Exists(launcher))
            throw new FileNotFoundException("Sanctuary launcher was not found while saving the display mode.", launcher);

        SafeFileSystem.RefuseSymbolicLink(launcherDir, "launcher directory");
        var destination = Path.Combine(launcherDir, FileName);
        SafeFileSystem.RefuseSymbolicLink(destination, "display mode configuration");
        var temporary = Path.Combine(launcherDir, $".{FileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, $"{mode}:{width}:{height}{Environment.NewLine}");
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }

        InstallerLog.Info($"Configured game display mode: {DisplayName(mode)} ({width}x{height})");
    }

    private static bool IsValidMode(string mode) => mode is Fullscreen or Windowed;
    private static bool IsValidResolution(int width, int height) => width is >= 640 and <= 16384 && height is >= 480 and <= 16384;
}
