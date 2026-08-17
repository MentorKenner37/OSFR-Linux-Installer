using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace OSFR.Linux.Installer.Services;

public static class InstallationOwnership
{
    public const string MarkerFileName = ".osfr-linux-install";
    public const string LegacyInstallInfoFileName = "install-info.txt";

    private const string ProductId = "SanctuaryLinuxInstaller";
    private const int CurrentFormatVersion = 2;
    private const string LauncherRelativePath = "Launcher/OSFRLauncher";

    private sealed record OwnershipDocument(
        string Product,
        int FormatVersion,
        string InstallId,
        string InstallRoot,
        string Launcher,
        string? InstallerVersion,
        string? LauncherSha256,
        DateTimeOffset CreatedUtc);

    public static void Write(string installRoot)
    {
        installRoot = InstallService.NormalizeInstallRoot(installRoot);
        SafeFileSystem.RefuseSymbolicLinkAncestors(installRoot, "installation root");

        var launcher = Path.Combine(installRoot, "Launcher", "OSFRLauncher");
        if (!Directory.Exists(installRoot) || InstallService.IsSymbolicLink(installRoot))
            throw new InvalidOperationException("Cannot record installation ownership for an invalid or symbolic-link install root.");
        if (!File.Exists(launcher) || InstallService.IsSymbolicLink(launcher))
            throw new InvalidOperationException("Cannot record installation ownership before the Sanctuary launcher has been verified.");

        var marker = Path.Combine(installRoot, MarkerFileName);
        SafeFileSystem.RefuseSymbolicLink(marker, "installation ownership marker");

        var document = new OwnershipDocument(
            ProductId,
            CurrentFormatVersion,
            Guid.NewGuid().ToString("D"),
            installRoot,
            LauncherRelativePath,
            GetInstallerVersion(),
            ComputeSha256(launcher),
            DateTimeOffset.UtcNow);

        WriteAtomically(marker, document);
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
                if (document is not null && IsValidDocument(document, installRoot, launcher))
                    return true;
            }
            catch (JsonException)
            {
                // Older releases wrote a plain-text marker. Validate it below.
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
            {
                InstallerLog.Warn($"Could not validate installation ownership marker {marker}: {ex.Message}");
                return false;
            }
        }

        return IsLegacyOwnedInstall(installRoot, marker, launcher);
    }

    public static bool TryMigrateLegacy(string installRoot)
    {
        installRoot = InstallService.NormalizeInstallRoot(installRoot);
        var launcher = Path.Combine(installRoot, "Launcher", "OSFRLauncher");
        var marker = Path.Combine(installRoot, MarkerFileName);

        if (!IsLegacyOwnedInstall(installRoot, marker, launcher))
            return false;

        try
        {
            Write(installRoot);
            InstallerLog.Info($"Migrated legacy Sanctuary installation ownership metadata in {installRoot}.");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or CryptographicException)
        {
            InstallerLog.Warn($"Legacy Sanctuary ownership metadata could not be migrated in {installRoot}: {ex.Message}");
            return false;
        }
    }

    private static bool IsValidDocument(OwnershipDocument document, string installRoot, string launcher)
    {
        if (!string.Equals(document.Product, ProductId, StringComparison.Ordinal) ||
            document.FormatVersion is < 1 or > CurrentFormatVersion ||
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

        if (!string.Equals(recordedRoot, installRoot, StringComparison.Ordinal))
            return false;

        if (document.FormatVersion >= 2)
        {
            if (string.IsNullOrWhiteSpace(document.LauncherSha256))
                return false;
            var actualHash = ComputeSha256(launcher);
            if (!string.Equals(actualHash, document.LauncherSha256, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
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
            return string.Equals(markerText, "Open Source Free Realms Linux Installer", StringComparison.Ordinal) ||
                   string.Equals(markerText, "Sanctuary Linux Installer", StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            InstallerLog.Warn($"Could not validate legacy installation marker {marker}: {ex.Message}");
            return false;
        }
    }

    private static string ComputeSha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string GetInstallerVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "unknown";
    }

    private static void WriteAtomically(string marker, OwnershipDocument document)
    {
        var directory = Path.GetDirectoryName(marker)!;
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, $".{Path.GetFileName(marker)}.{Guid.NewGuid():N}.tmp");

        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(document);
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            if (OperatingSystem.IsLinux())
                File.SetUnixFileMode(temp, UnixFileMode.UserRead | UnixFileMode.UserWrite);

            SafeFileSystem.RefuseSymbolicLink(marker, "installation ownership marker");
            File.Move(temp, marker, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }
}