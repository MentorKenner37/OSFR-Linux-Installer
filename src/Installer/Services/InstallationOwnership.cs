using System.Text.Json;

namespace OSFR.Linux.Installer.Services;

public static class InstallationOwnership
{
    public const string MarkerFileName = ".osfr-linux-install";
    public const string LegacyInstallInfoFileName = "install-info.txt";

    private const string ProductId = "SanctuaryLinuxInstaller";
    private const int CurrentFormatVersion = 1;
    private const string LauncherRelativePath = "Launcher/OSFRLauncher";

    private sealed record OwnershipDocument(
        string Product,
        int FormatVersion,
        string InstallId,
        string InstallRoot,
        string Launcher,
        DateTimeOffset CreatedUtc);

    public static void Write(string installRoot)
    {
        installRoot = InstallService.NormalizeInstallRoot(installRoot);
        var launcher = Path.Combine(installRoot, "Launcher", "OSFRLauncher");
        if (!Directory.Exists(installRoot) || InstallService.IsSymbolicLink(installRoot))
            throw new InvalidOperationException("Cannot record installation ownership for an invalid or symbolic-link install root.");
        if (!File.Exists(launcher) || InstallService.IsSymbolicLink(launcher))
            throw new InvalidOperationException("Cannot record installation ownership before the Sanctuary launcher has been verified.");

        var marker = Path.Combine(installRoot, MarkerFileName);
        if (InstallService.IsSymbolicLink(marker))
            throw new InvalidOperationException("Refusing to write the installation ownership marker through a symbolic link.");

        var document = new OwnershipDocument(
            ProductId,
            CurrentFormatVersion,
            Guid.NewGuid().ToString("D"),
            installRoot,
            LauncherRelativePath,
            DateTimeOffset.UtcNow);

        var temp = marker + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(document));
        File.Move(temp, marker, overwrite: true);

        if (OperatingSystem.IsLinux())
            File.SetUnixFileMode(marker, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    public static bool IsOwned(string installRoot)
    {
        if (!Directory.Exists(installRoot) || InstallService.IsSymbolicLink(installRoot))
            return false;

        installRoot = InstallService.NormalizeInstallRoot(installRoot);
        var launcher = Path.Combine(installRoot, "Launcher", "OSFRLauncher");
        if (!File.Exists(launcher) || InstallService.IsSymbolicLink(launcher))
            return false;

        var marker = Path.Combine(installRoot, MarkerFileName);
        if (File.Exists(marker) && !InstallService.IsSymbolicLink(marker))
        {
            try
            {
                var document = JsonSerializer.Deserialize<OwnershipDocument>(File.ReadAllText(marker));
                if (document is not null && IsValidDocument(document, installRoot))
                    return true;
            }
            catch (JsonException)
            {
                // Older releases wrote a plain-text marker. Validate it only with the legacy install-info file below.
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                InstallerLog.Warn($"Could not validate installation ownership marker {marker}: {ex.Message}");
                return false;
            }
        }

        return IsLegacyOwnedInstall(installRoot, marker, launcher);
    }

    private static bool IsValidDocument(OwnershipDocument document, string installRoot)
    {
        if (!string.Equals(document.Product, ProductId, StringComparison.Ordinal) ||
            document.FormatVersion != CurrentFormatVersion ||
            !Guid.TryParse(document.InstallId, out _) ||
            !string.Equals(document.Launcher, LauncherRelativePath, StringComparison.Ordinal))
            return false;

        string recordedRoot;
        try
        {
            recordedRoot = InstallService.NormalizeInstallRoot(document.InstallRoot);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            InstallerLog.Warn($"Ownership marker contains an invalid install root: {ex.Message}");
            return false;
        }

        return string.Equals(recordedRoot, installRoot, StringComparison.Ordinal);
    }

    private static bool IsLegacyOwnedInstall(string installRoot, string marker, string launcher)
    {
        var info = Path.Combine(installRoot, LegacyInstallInfoFileName);
        if (!File.Exists(info) || InstallService.IsSymbolicLink(info) || !File.Exists(launcher) || InstallService.IsSymbolicLink(launcher))
            return false;

        if (!File.Exists(marker))
            return true;
        if (InstallService.IsSymbolicLink(marker))
            return false;

        try
        {
            var markerText = File.ReadAllText(marker).Trim();
            return string.Equals(markerText, "Open Source Free Realms Linux Installer", StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            InstallerLog.Warn($"Could not validate legacy installation marker {marker}: {ex.Message}");
            return false;
        }
    }
}
