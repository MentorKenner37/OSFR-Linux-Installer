using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;

namespace OSFR.Linux.Installer.Services;

public sealed record InstallProgress(int Percent, string Message);
public sealed record InstallOptions(bool CreateDesktopShortcut = true, bool RepairExisting = false);
public sealed record UninstallOptions(bool RemoveUserData = false);
public enum InstallationCondition { NotInstalled, Installed, NeedsRepair }
public sealed record InstallationInfo(InstallationCondition Condition, string? Version, bool HasDesktopShortcut);

public sealed class InstallService
{
    private const string PayloadResource = "OSFR.Linux.Installer.Payload";
    private const string DesktopFileName = "OSFR-Linux.desktop";
    private const string DesktopIconName = "osfr-linux";
    private const string TransactionStateFileName = ".sanctuary-install-transaction.json";
    private const string LegacyGamescopeOwnershipFileName = ".gamescope-installed-by-sanctuary.json";

    public static string DefaultInstallRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".local", "share", "OSFR-Linux");

    public static string LauncherDataRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".local", "share", "OSFRLauncher");

    public static string DesktopIconPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".local", "share", "icons", "hicolor", "256x256", "apps", $"{DesktopIconName}.png");

    public static string DesktopShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop", DesktopFileName);

    public static string NormalizeInstallRoot(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        path = Environment.ExpandEnvironmentVariables(path.Trim());

        if (path == "~")
            path = home;
        else if (path.StartsWith("~/", StringComparison.Ordinal))
            path = Path.Combine(home, path[2..]);

        return Path.GetFullPath(path);
    }

    public static string? GetInstallDestinationError(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "Choose a dedicated folder for Sanctuary.";

        string installRoot;
        try
        {
            installRoot = NormalizeInstallRoot(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return "The installation path is not valid.";
        }

        var home = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        var root = Path.GetPathRoot(installRoot);

        if (string.Equals(installRoot, root, StringComparison.Ordinal) ||
            string.Equals(installRoot, home, StringComparison.Ordinal))
            return "Choose a dedicated Sanctuary folder instead of the filesystem root or your home folder.";

        if (File.Exists(installRoot))
            return "The selected path is a file. Choose a folder instead.";

        if (IsSymbolicLink(installRoot))
            return "The installation folder cannot be a symbolic link.";

        if (SafeFileSystem.HasSymbolicLinkAncestor(installRoot))
            return "The installation path contains a symbolic-link directory. Choose a direct filesystem path instead.";

        if (File.Exists(Path.Combine(installRoot, TransactionStateFileName)))
            return null;

        if (!Directory.Exists(installRoot) || IsOwnedInstallRoot(installRoot))
            return null;

        try
        {
            if (Directory.EnumerateFileSystemEntries(installRoot).Any())
                return "This folder already contains files. Choose an empty folder or an existing Sanctuary installation.";
        }
        catch (UnauthorizedAccessException)
        {
            return "The selected folder cannot be read with your current permissions.";
        }
        catch (IOException)
        {
            return "The selected folder cannot be accessed.";
        }

        return null;
    }

    public bool IsInstalled(string installRoot)
    {
        installRoot = NormalizeInstallRoot(installRoot);
        var owned = IsOwnedInstallRoot(installRoot) && File.Exists(Path.Combine(installRoot, "Launcher", "OSFRLauncher"));
        if (owned)
            InstallationOwnership.TryMigrateLegacy(installRoot);
        return owned;
    }

    public InstallationInfo GetInstallationInfo(string installRoot)
    {
        installRoot = NormalizeInstallRoot(installRoot);
        if (IsInstalled(installRoot))
            return new(InstallationCondition.Installed, InstallationOwnership.GetInstalledVersion(installRoot), File.Exists(DesktopShortcutPath));

        return InstallationOwnership.HasRecognizableMetadata(installRoot)
            ? new(InstallationCondition.NeedsRepair, InstallationOwnership.GetInstalledVersion(installRoot), File.Exists(DesktopShortcutPath))
            : new(InstallationCondition.NotInstalled, null, File.Exists(DesktopShortcutPath));
    }

    public async Task InstallAsync(
        string installRoot,
        SystemState state,
        IProgress<InstallProgress> progress,
        InstallOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new InstallOptions();
        if (!state.Ready || state.SteamRoot is null || state.ProtonPath is null || !state.ProtonCompatible)
            throw new InvalidOperationException("Linux, x86_64, Steam and a compatible Proton runtime are required.");

        installRoot = NormalizeInstallRoot(installRoot);
        SafeFileSystem.RefuseSymbolicLinkAncestors(installRoot, "installation root");
        InstallationTransaction.RecoverIfNeeded(installRoot);
        if (!(options.RepairExisting && InstallationOwnership.HasRecognizableMetadata(installRoot)))
            ValidateInstallDestination(installRoot);

        var hadExistingInstall = IsOwnedInstallRoot(installRoot);
        var launcherDir = Path.Combine(installRoot, "Launcher");
        var prefixDir = Path.Combine(installRoot, "ProtonPrefix");
        var stagingDir = Path.Combine(installRoot, $".launcher-staging-{Guid.NewGuid():N}");
        var transaction = new InstallationTransaction(installRoot);
        var transactionStarted = false;
        var desktopTouched = false;

        InstallerLog.Info($"Starting transactional installation to {installRoot} using Proton {state.ProtonPath}");

        try
        {
            progress.Report(new(5, "Preparing installation..."));
            Directory.CreateDirectory(installRoot);
            SafeFileSystem.RefuseSymbolicLink(installRoot, "installation folder");

            Directory.CreateDirectory(prefixDir);
            SafeFileSystem.RefuseSymbolicLink(prefixDir, "Proton prefix folder");

            progress.Report(new(15, "Extracting Sanctuary launcher..."));
            Directory.CreateDirectory(stagingDir);
            await ExtractLauncherPayloadAsync(stagingDir, cancellationToken);

            var stagedLauncher = Path.Combine(stagingDir, "OSFRLauncher");
            if (!File.Exists(stagedLauncher))
                throw new InvalidDataException("The embedded launcher payload did not contain OSFRLauncher.");
            SafeFileSystem.EnsureExecutable(stagedLauncher, "Sanctuary launcher");
            VerifyLauncher(stagingDir, stagedLauncher);

            cancellationToken.ThrowIfCancellationRequested();
            transaction.Begin();
            transactionStarted = true;
            transaction.Promote(stagingDir);

            var launcher = Path.Combine(launcherDir, "OSFRLauncher");

            progress.Report(new(55, "Configuring Steam Proton..."));
            await File.WriteAllTextAsync(Path.Combine(launcherDir, "proton-path.txt"), state.ProtonPath + Environment.NewLine, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(launcherDir, "steam-path.txt"), state.SteamRoot + Environment.NewLine, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(launcherDir, "prefix-path.txt"), prefixDir + Environment.NewLine, cancellationToken);
            VerifyLauncher(launcherDir, launcher);

            var info = $"""
                       Sanctuary Linux Installation

                       Launcher: {launcher}
                       Steam: {state.SteamRoot}
                       Proton: {state.ProtonPath}
                       Proton Prefix: {prefixDir}
                       """;
            await File.WriteAllTextAsync(Path.Combine(installRoot, InstallationOwnership.LegacyInstallInfoFileName), info, cancellationToken);

            progress.Report(new(70, "Creating desktop integration..."));
            await InstallDesktopIconAsync(cancellationToken);
            CreateDesktopEntries(launcher, options.CreateDesktopShortcut);
            desktopTouched = true;
            RefreshDesktopIntegration();

            progress.Report(new(85, "Verifying installation..."));
            VerifyLauncher(launcherDir, launcher);

            // Final commit point: do not recognize the new launcher as owned until every install step above succeeded.
            InstallationOwnership.Write(installRoot);
            if (!InstallationOwnership.IsOwned(installRoot))
                throw new InvalidOperationException("Installation ownership verification failed.");

            try
            {
                InstallerState.SaveInstallRoot(installRoot);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                InstallerLog.Warn($"Installation succeeded but the custom install location could not be remembered: {ex.Message}");
            }

            transaction.Commit();
            transactionStarted = false;

            // beta.8 no longer manages or launches Gamescope. Remove only Sanctuary's obsolete
            // metadata; never alter a system Gamescope package or the user's APT configuration.
            TryDeleteFile(Path.Combine(installRoot, LegacyGamescopeOwnershipFileName));

            progress.Report(new(100, "Installation complete"));
            InstallerLog.Info("Transactional installation completed successfully.");
        }
        catch (Exception ex)
        {
            InstallerLog.Error("Installation failed; attempting rollback", ex);
            if (transactionStarted)
            {
                try
                {
                    transaction.Rollback();
                    InstallerLog.Info("Previous Sanctuary launcher restored successfully.");
                }
                catch (Exception rollbackEx)
                {
                    throw new AggregateException("Installation failed and rollback also failed. See the installer log for recovery details.", ex, rollbackEx);
                }
            }

            if (!hadExistingInstall)
            {
                TryDeleteFile(Path.Combine(installRoot, InstallationOwnership.MarkerFileName));
                TryDeleteFile(Path.Combine(installRoot, InstallationOwnership.LegacyInstallInfoFileName));
                if (Directory.Exists(prefixDir) || IsSymbolicLink(prefixDir))
                {
                    try { SafeFileSystem.DeleteDirectoryTreeNoFollow(prefixDir); }
                    catch (Exception cleanupEx) when (cleanupEx is IOException or UnauthorizedAccessException)
                    {
                        InstallerLog.Warn($"Could not remove incomplete Proton prefix {prefixDir}: {cleanupEx.Message}");
                    }
                }
                if (desktopTouched)
                    RemoveDesktopIntegration();
            }

            throw;
        }
        finally
        {
            if (Directory.Exists(stagingDir) || IsSymbolicLink(stagingDir))
            {
                try { SafeFileSystem.DeleteDirectoryTreeNoFollow(stagingDir); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    InstallerLog.Warn($"Could not remove temporary staging directory {stagingDir}: {ex.Message}");
                }
            }
        }
    }

    public Task UninstallAsync(
        string installRoot,
        IProgress<InstallProgress> progress,
        UninstallOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new UninstallOptions();
        installRoot = NormalizeInstallRoot(installRoot);
        SafeFileSystem.RefuseSymbolicLinkAncestors(installRoot, "installation root");
        InstallationTransaction.RecoverIfNeeded(installRoot);

        if (IsSymbolicLink(installRoot))
            throw new InvalidOperationException("The selected installation folder is a symbolic link and will not be recursively deleted.");
        if (Directory.Exists(installRoot) && !IsOwnedInstallRoot(installRoot))
            throw new InvalidOperationException("The selected folder is not recognized as a Sanctuary Linux installation, so it will not be deleted.");

        return Task.Run(() =>
        {
            InstallerLog.Info($"Starting uninstall from {installRoot}");
            progress.Report(new(10, "Preparing Sanctuary removal..."));

            var home = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            var targets = new List<(string Path, bool RequireHome)>
            {
                (Path: installRoot, RequireHome: false),
                (Path: Path.Combine(home, ".local", "share", "applications", DesktopFileName), RequireHome: true),
                (Path: DesktopShortcutPath, RequireHome: true),
                (Path: DesktopIconPath, RequireHome: true)
            };

            if (options.RemoveUserData)
            {
                targets.Add((LauncherDataRoot, true));
                targets.Add((InstallerState.StateDirectory, true));
            }

            for (var i = 0; i < targets.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var target = targets[i];
                var percent = 20 + (int)(70.0 * (i + 1) / targets.Count);
                progress.Report(new(percent, $"Removing {Path.GetFileName(target.Path)}..."));

                if (target.RequireHome && !IsPathInside(target.Path, home))
                    throw new InvalidOperationException($"Refusing to delete a path outside the user home directory: {target.Path}");

                try
                {
                    if (Directory.Exists(target.Path) || IsSymbolicLink(target.Path))
                        SafeFileSystem.DeleteDirectoryTreeNoFollow(target.Path);
                    else if (File.Exists(target.Path))
                        File.Delete(target.Path);
                }
                catch (DirectoryNotFoundException) { }
                catch (FileNotFoundException) { }
            }

            InstallerState.ClearInstallRoot(installRoot);
            RefreshDesktopIntegration();
            progress.Report(new(100, "Uninstallation complete"));
            InstallerLog.Info("Uninstallation completed successfully.");
        }, cancellationToken);
    }

    public void Launch(string installRoot)
    {
        installRoot = NormalizeInstallRoot(installRoot);
        var launcher = Path.Combine(installRoot, "Launcher", "OSFRLauncher");
        if (!File.Exists(launcher))
            throw new FileNotFoundException("Sanctuary launcher was not found.", launcher);
        SafeFileSystem.RefuseSymbolicLink(launcher, "Sanctuary launcher executable");

        InstallerLog.Info($"Launching Sanctuary from {launcher}");
        Process.Start(new ProcessStartInfo
        {
            FileName = launcher,
            WorkingDirectory = Path.GetDirectoryName(launcher)!,
            UseShellExecute = false
        });
    }

    public void SetDesktopShortcut(string installRoot, bool enabled)
    {
        installRoot = NormalizeInstallRoot(installRoot);
        if (!IsInstalled(installRoot))
            throw new InvalidOperationException("A verified Sanctuary installation is required before changing its shortcut.");
        var launcher = Path.Combine(installRoot, "Launcher", "OSFRLauncher");
        CreateDesktopEntries(launcher, enabled);
        RefreshDesktopIntegration();
    }

    public static bool IsPathInside(string path, string parent) => SafeFileSystem.IsPathInside(path, parent);
    public static bool IsSymbolicLink(string path) => SafeFileSystem.IsSymbolicLink(path);
    public static bool HasSymbolicLinkAncestor(string path) => SafeFileSystem.HasSymbolicLinkAncestor(path);
    public static bool IsSafeArchiveEntry(string entryName) => SafeFileSystem.IsSafeArchiveEntry(entryName);

    private static void ValidateInstallDestination(string installRoot)
    {
        var error = GetInstallDestinationError(installRoot);
        if (error is not null)
            throw new InvalidOperationException(error);
    }

    private static bool IsOwnedInstallRoot(string installRoot) => InstallationOwnership.IsOwned(installRoot);

    private static async Task ExtractLauncherPayloadAsync(string stagingDir, CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetExecutingAssembly();
        await using var payload = assembly.GetManifestResourceStream(PayloadResource)
            ?? throw new InvalidOperationException("This installer build does not contain the launcher payload. Download the packaged installer from this repository's GitHub Releases page.");

        using var archive = new ZipArchive(payload, ZipArchiveMode.Read, leaveOpen: false);
        var root = Path.GetFullPath(stagingDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!SafeFileSystem.IsSafeArchiveEntry(entry.FullName))
                throw new InvalidDataException($"Unsafe path in launcher payload: {entry.FullName}");

            var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
            if (unixType == 0xA000)
                throw new InvalidDataException($"Symbolic links are not allowed in the launcher payload: {entry.FullName}");

            var destination = Path.GetFullPath(Path.Combine(stagingDir, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
            if (!destination.StartsWith(root, StringComparison.Ordinal))
                throw new InvalidDataException("Invalid path in launcher payload.");

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destination);
                SafeFileSystem.RefuseSymbolicLink(destination, "launcher payload directory");
                continue;
            }

            var parent = Path.GetDirectoryName(destination)!;
            Directory.CreateDirectory(parent);
            SafeFileSystem.RefuseSymbolicLink(parent, "launcher payload directory");

            await using var source = entry.Open();
            await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await source.CopyToAsync(output, cancellationToken);
        }
    }

    private static async Task InstallDesktopIconAsync(CancellationToken cancellationToken)
    {
        var iconDirectory = Path.GetDirectoryName(DesktopIconPath)!;
        Directory.CreateDirectory(iconDirectory);
        SafeFileSystem.RefuseSymbolicLink(iconDirectory, "icon-theme directory");
        SafeFileSystem.RefuseSymbolicLink(DesktopIconPath, "desktop icon file");

        var assembly = Assembly.GetExecutingAssembly();
        await using var stream = assembly.GetManifestResourceStream("OSFR.Linux.Installer.Icon")
            ?? throw new InvalidOperationException("The installer icon resource is missing.");
        await using var output = new FileStream(DesktopIconPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(output, cancellationToken);
    }

    private static void CreateDesktopEntries(string launcher, bool createDesktopShortcut)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var applications = Path.Combine(home, ".local", "share", "applications");
        Directory.CreateDirectory(applications);
        SafeFileSystem.RefuseSymbolicLink(applications, "applications directory");

        var desktopEntry = $"""
                           [Desktop Entry]
                           Type=Application
                           Name=Sanctuary
                           Comment=Sanctuary - Open Source Free Realms
                           Exec={QuoteDesktopExecPath(launcher)}
                           Icon={DesktopIconName}
                           Terminal=false
                           Categories=Game;
                           StartupNotify=true
                           StartupWMClass=OSFRLauncher
                           X-GNOME-WMClass=OSFRLauncher
                           """;

        var applicationFile = Path.Combine(applications, DesktopFileName);
        File.WriteAllText(applicationFile, desktopEntry);
        SafeFileSystem.EnsureExecutable(applicationFile, "application-menu shortcut");

        var desktop = Path.Combine(home, "Desktop");
        if (createDesktopShortcut && Directory.Exists(desktop) && !IsSymbolicLink(desktop))
        {
            File.WriteAllText(DesktopShortcutPath, desktopEntry);
            SafeFileSystem.EnsureExecutable(DesktopShortcutPath, "desktop shortcut");
        }
        else if (!createDesktopShortcut)
            TryDeleteFile(DesktopShortcutPath);
    }

    private static void RemoveDesktopIntegration()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        TryDeleteFile(Path.Combine(home, ".local", "share", "applications", DesktopFileName));
        TryDeleteFile(Path.Combine(home, "Desktop", DesktopFileName));
        TryDeleteFile(DesktopIconPath);
        RefreshDesktopIntegration();
    }

    private static void RefreshDesktopIntegration()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        RunOptionalCommand("update-desktop-database", Path.Combine(home, ".local", "share", "applications"));
        RunOptionalCommand("gtk-update-icon-cache", "-f", "-t", Path.Combine(home, ".local", "share", "icons", "hicolor"));
    }

    private static void RunOptionalCommand(string fileName, params string[] arguments)
    {
        if (!OperatingSystem.IsLinux())
            return;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo);
            process?.WaitForExit(5000);
        }
        catch (Win32Exception)
        {
            // Optional desktop cache utility is not installed.
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            InstallerLog.Warn($"Could not refresh desktop integration with {fileName}: {ex.Message}");
        }
    }

    private static string QuoteDesktopExecPath(string value)
    {
        var escaped = value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("`", "\\`")
            .Replace("$", "\\$")
            .Replace("%", "%%");
        return $"\"{escaped}\"";
    }

    private static void VerifyLauncher(string launcherDir, string launcher)
    {
        if (!File.Exists(launcher) || IsSymbolicLink(launcher))
            throw new FileNotFoundException("Sanctuary launcher verification failed.", launcher);

        if (OperatingSystem.IsLinux() && (File.GetUnixFileMode(launcher) & UnixFileMode.UserExecute) == 0)
            throw new InvalidOperationException("Sanctuary launcher verification failed because OSFRLauncher is not executable.");

        var skia = Path.Combine(launcherDir, "libSkiaSharp.so");
        if (!File.Exists(skia) || IsSymbolicLink(skia))
            throw new FileNotFoundException("Linux x64 libSkiaSharp.so is missing from the launcher payload.", skia);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path) && !IsSymbolicLink(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            InstallerLog.Warn($"Could not remove {path}: {ex.Message}");
        }
    }
}
