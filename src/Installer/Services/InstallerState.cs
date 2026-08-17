using System.Text.Json;

namespace OSFR.Linux.Installer.Services;

public static class InstallerState
{
    private sealed record StateDocument(int FormatVersion, string? InstallRoot);

    private const int CurrentFormatVersion = 1;

    public static string StateDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".local", "state", "OSFR-Linux");

    public static string StatePath => Path.Combine(StateDirectory, "installer-state.json");

    public static string GetInitialInstallRoot()
    {
        var remembered = LoadInstallRoot();
        if (string.IsNullOrWhiteSpace(remembered))
            return InstallService.DefaultInstallRoot;

        try
        {
            var normalized = InstallService.NormalizeInstallRoot(remembered);
            var error = InstallService.GetInstallDestinationError(normalized);
            if (error is null)
                return normalized;

            InstallerLog.Warn($"Ignoring remembered install root {normalized}: {error}");
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException or UnauthorizedAccessException)
        {
            InstallerLog.Warn($"Ignoring invalid remembered install root: {ex.Message}");
        }

        return InstallService.DefaultInstallRoot;
    }

    public static string? LoadInstallRoot()
    {
        try
        {
            if (!File.Exists(StatePath) || InstallService.IsSymbolicLink(StatePath))
                return null;

            var document = JsonSerializer.Deserialize<StateDocument>(File.ReadAllText(StatePath));
            return document?.FormatVersion == CurrentFormatVersion ? document.InstallRoot : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            InstallerLog.Warn($"Could not read installer state: {ex.Message}");
            return null;
        }
    }

    public static void SaveInstallRoot(string installRoot)
    {
        installRoot = InstallService.NormalizeInstallRoot(installRoot);
        Directory.CreateDirectory(StateDirectory);

        if (InstallService.IsSymbolicLink(StateDirectory) || InstallService.IsSymbolicLink(StatePath))
            throw new InvalidOperationException("Refusing to store installer state through a symbolic link.");

        var document = new StateDocument(CurrentFormatVersion, installRoot);
        var tempPath = StatePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(document));
        File.Move(tempPath, StatePath, overwrite: true);

        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(
                StateDirectory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            File.SetUnixFileMode(StatePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    public static void ClearInstallRoot(string installRoot)
    {
        var remembered = LoadInstallRoot();
        if (string.IsNullOrWhiteSpace(remembered))
            return;

        string normalizedRemembered;
        string normalizedRequested;
        try
        {
            normalizedRemembered = InstallService.NormalizeInstallRoot(remembered);
            normalizedRequested = InstallService.NormalizeInstallRoot(installRoot);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            InstallerLog.Warn($"Could not compare installer state during uninstall: {ex.Message}");
            return;
        }

        if (!string.Equals(normalizedRemembered, normalizedRequested, StringComparison.Ordinal))
            return;

        try
        {
            if (File.Exists(StatePath) && !InstallService.IsSymbolicLink(StatePath))
                File.Delete(StatePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            InstallerLog.Warn($"Could not clear installer state: {ex.Message}");
        }
    }
}
