using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;

namespace OSFR.Linux.Installer.Services;

public sealed record InstallProgress(int Percent, string Message);

public sealed class InstallService
{
    private const string PayloadResource = "OSFR.Linux.Installer.Payload";
    private const string OwnershipMarker = ".osfr-linux-install";
    private const string LegacyInstallInfo = "install-info.txt";
    private const string DesktopFileName = "OSFR-Linux.desktop";
    private const string DesktopIconName = "osfr-linux";

    public static string DefaultInstallRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".local", "share", "OSFR-Linux");

    public static string LauncherDataRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".local", "share", "OSFRLauncher");

    public static string DesktopIconPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".local", "share", "icons", "hicolor", "256x256", "apps", $"{DesktopIconName}.png");

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
            return "Choose a dedicated folder for OSFR.";

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
        {
            return "Choose a dedicated OSFR folder instead of the filesystem root or your home folder.";
        }

        if (File.Exists(installRoot))
            return "The selected path is a file. Choose a folder instead.";

        if (IsSymbolicLink(installRoot))
            return "The installation folder cannot be a symbolic link.";

        if (!Directory.Exists(installRoot) || IsOwnedInstallRoot(installRoot))
            return null;

        try
        {
            if (Directory.EnumerateFileSystemEntries(installRoot).Any())
                return "This folder already contains files. Choose an empty folder or an existing OSFR installation.";
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
        return IsOwnedInstallRoot(installRoot) &&
               File.Exists(Path.Combine(installRoot, "Launcher", "OSFRLauncher"));
    }

    public async Task InstallAsync(
        string installRoot,
        SystemState state,
        IProgress<InstallProgress> progress,
        CancellationToken cancellationToken = default)
    {
        if (!state.Ready || state.SteamRoot is null || state.ProtonPath is null)
            throw new InvalidOperationException("Linux, x86_64, Steam and Proton are required.");

        installRoot = NormalizeInstallRoot(installRoot);
        ValidateInstallDestination(installRoot);
        InstallerLog.Info($"Starting installation to {installRoot} using Proton {state.ProtonPath}");

        var launcherDir = Path.Combine(installRoot, "Launcher");
        var prefixDir = Path.Combine(installRoot, "ProtonPrefix");
        var stagingDir = Path.Combine(installRoot, $".launcher-staging-{Guid.NewGuid():N}");

        try
        {
            progress.Report(new(5, "Preparing installation..."));
            Directory.CreateDirectory(installRoot);
            RefuseSymbolicLink(installRoot, "installation folder");

            await File.WriteAllTextAsync(
                Path.Combine(installRoot, OwnershipMarker),
                "Open Source Free Realms Linux Installer\n",
                cancellationToken);

            Directory.CreateDirectory(prefixDir);
            RefuseSymbolicLink(prefixDir, "Proton prefix folder");

            progress.Report(new(15, "Extracting OSFR Launcher..."));
            Directory.CreateDirectory(stagingDir);
            await ExtractLauncherPayloadAsync(stagingDir, cancellationToken);

            var stagedLauncher = Path.Combine(stagingDir, "OSFRLauncher");
            if (!File.Exists(stagedLauncher))
                throw new InvalidDataException("The embedded launcher payload did not contain OSFRLauncher.");
            EnsureExecutable(stagedLauncher, "OSFR Launcher");
            VerifyLauncher(stagingDir, stagedLauncher);

            if (Directory.Exists(launcherDir) || IsSymbolicLink(launcherDir))
                DeleteDirectoryTreeNoFollow(launcherDir);
            Directory.Move(stagingDir, launcherDir);

            var launcher = Path.Combine(launcherDir, "OSFRLauncher");

            progress.Report(new(55, "Configuring Steam Proton..."));
            await File.WriteAllTextAsync(Path.Combine(launcherDir, "proton-path.txt"), state.ProtonPath + Environment.NewLine, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(launcherDir, "steam-path.txt"), state.SteamRoot + Environment.NewLine, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(launcherDir, "prefix-path.txt"), prefixDir + Environment.NewLine, cancellationToken);

            progress.Report(new(70, "Creating desktop integration..."));
            await InstallDesktopIconAsync(cancellationToken);
            CreateDesktopEntries(launcher);
            RefreshDesktopIntegration();

            progress.Report(new(85, "Verifying installation..."));
            VerifyLauncher(launcherDir, launcher);

            var info = $"""
                       OSFR Linux Installation

                       Launcher: {launcher}
                       Steam: {state.SteamRoot}
                       Proton: {state.ProtonPath}
                       Proton Prefix: {prefixDir}
                       """;
            await File.WriteAllTextAsync(Path.Combine(installRoot, LegacyInstallInfo), info, cancellationToken);

            progress.Report(new(100, "Installation complete"));
            InstallerLog.Info("Installation completed successfully.");
        }
        catch (Exception ex)
        {
            InstallerLog.Error("Installation failed", ex);
            throw;
        }
        finally
        {
            if (Directory.Exists(stagingDir) || IsSymbolicLink(stagingDir))
            {
                try { DeleteDirectoryTreeNoFollow(stagingDir); }
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
        CancellationToken cancellationToken = default)
    {
        installRoot = NormalizeInstallRoot(installRoot);

        if (IsSymbolicLink(installRoot))
            throw new InvalidOperationException("The selected installation folder is a symbolic link and will not be recursively deleted.");

        if (Directory.Exists(installRoot) && !IsOwnedInstallRoot(installRoot))
            throw new InvalidOperationException("The selected folder is not recognized as an OSFR Linux installation, so it will not be deleted.");

        return Task.Run(() =>
        {
            InstallerLog.Info($"Starting uninstall from {installRoot}");
            progress.Report(new(10, "Preparing OSFR removal..."));

            var home = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            var targets = new[]
            {
                (Path: installRoot, RequireHome: false),
                (Path: LauncherDataRoot, RequireHome: true),
                (Path: Path.Combine(home, ".cache", "OSFRLauncher"), RequireHome: true),
                (Path: Path.Combine(home, ".cache", "OSFR-Linux"), RequireHome: true),
                (Path: Path.Combine(home, ".local", "share", "applications", DesktopFileName), RequireHome: true),
                (Path: Path.Combine(home, "Desktop", DesktopFileName), RequireHome: true),
                (Path: DesktopIconPath, RequireHome: true)
            };

            for (var i = 0; i < targets.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var target = targets[i];
                var percent = 20 + (int)(70.0 * (i + 1) / targets.Length);
                progress.Report(new(percent, $"Removing {Path.GetFileName(target.Path)}..."));

                if (target.RequireHome && !IsPathInside(target.Path, home))
                    throw new InvalidOperationException($"Refusing to delete a path outside the user home directory: {target.Path}");

                try
                {
                    if (Directory.Exists(target.Path) || IsSymbolicLink(target.Path))
                        DeleteDirectoryTreeNoFollow(target.Path);
                    else if (File.Exists(target.Path))
                        File.Delete(target.Path);
                }
                catch (DirectoryNotFoundException) { }
                catch (FileNotFoundException) { }
            }

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
            throw new FileNotFoundException("OSFRLauncher was not found.", launcher);
        RefuseSymbolicLink(launcher, "OSFR Launcher executable");

        InstallerLog.Info($"Launching OSFR Launcher from {launcher}");
        Process.Start(new ProcessStartInfo
        {
            FileName = launcher,
            WorkingDirectory = Path.GetDirectoryName(launcher)!,
            UseShellExecute = false
        });
    }

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

    public static bool IsSafeArchiveEntry(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName) || Path.IsPathRooted(entryName) || entryName.StartsWith("/", StringComparison.Ordinal) || entryName.StartsWith("\\", StringComparison.Ordinal))
            return false;

        var normalized = entryName.Replace('\\', '/');
        return normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .All(segment => segment is not "." and not "..");
    }

    private static void ValidateInstallDestination(string installRoot)
    {
        var error = GetInstallDestinationError(installRoot);
        if (error is not null)
            throw new InvalidOperationException(error);
    }

    private static bool IsOwnedInstallRoot(string installRoot)
    {
        if (!Directory.Exists(installRoot) || IsSymbolicLink(installRoot))
            return false;

        var marker = Path.Combine(installRoot, OwnershipMarker);
        if (File.Exists(marker) && !IsSymbolicLink(marker))
            return true;

        var info = Path.Combine(installRoot, LegacyInstallInfo);
        var launcher = Path.Combine(installRoot, "Launcher", "OSFRLauncher");
        return File.Exists(info) && !IsSymbolicLink(info) && File.Exists(launcher) && !IsSymbolicLink(launcher);
    }

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
            if (!IsSafeArchiveEntry(entry.FullName))
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
                RefuseSymbolicLink(destination, "launcher payload directory");
                continue;
            }

            var parent = Path.GetDirectoryName(destination)!;
            Directory.CreateDirectory(parent);
            RefuseSymbolicLink(parent, "launcher payload directory");

            await using var source = entry.Open();
            await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await source.CopyToAsync(output, cancellationToken);
        }
    }

    private static async Task InstallDesktopIconAsync(CancellationToken cancellationToken)
    {
        var iconDirectory = Path.GetDirectoryName(DesktopIconPath)!;
        Directory.CreateDirectory(iconDirectory);
        RefuseSymbolicLink(iconDirectory, "icon-theme directory");
        RefuseSymbolicLink(DesktopIconPath, "desktop icon file");

        var assembly = Assembly.GetExecutingAssembly();
        await using var stream = assembly.GetManifestResourceStream("OSFR.Linux.Installer.Icon")
            ?? throw new InvalidOperationException("The installer icon resource is missing.");
        await using var output = new FileStream(DesktopIconPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(output, cancellationToken);
    }

    private static void CreateDesktopEntries(string launcher)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var applications = Path.Combine(home, ".local", "share", "applications");
        Directory.CreateDirectory(applications);
        RefuseSymbolicLink(applications, "applications directory");

        var desktopEntry = $"""
                           [Desktop Entry]
                           Type=Application
                           Name=Open Source Free Realms
                           Comment=Open Source Free Realms - Linux
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
        EnsureExecutable(applicationFile, "application-menu shortcut");

        var desktop = Path.Combine(home, "Desktop");
        if (Directory.Exists(desktop) && !IsSymbolicLink(desktop))
        {
            var desktopFile = Path.Combine(desktop, DesktopFileName);
            File.WriteAllText(desktopFile, desktopEntry);
            EnsureExecutable(desktopFile, "desktop shortcut");
        }
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
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                ArgumentList = { }
            });

            if (process is null)
                return;

            foreach (var argument in arguments)
                process.StartInfo.ArgumentList.Add(argument);
        }
        catch (Win32Exception)
        {
            // Optional desktop cache utility is not installed.
        }
        catch (IOException ex)
        {
            InstallerLog.Warn($"Could not refresh desktop integration with {fileName}: {ex.Message}");
        }
        catch (InvalidOperationException ex)
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

    private static void EnsureExecutable(string path, string description)
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

    private static void VerifyLauncher(string launcherDir, string launcher)
    {
        if (!File.Exists(launcher) || IsSymbolicLink(launcher))
            throw new FileNotFoundException("Launcher verification failed.", launcher);

        if (OperatingSystem.IsLinux() && (File.GetUnixFileMode(launcher) & UnixFileMode.UserExecute) == 0)
            throw new InvalidOperationException("Launcher verification failed because OSFRLauncher is not executable.");

        var skia = Path.Combine(launcherDir, "libSkiaSharp.so");
        if (!File.Exists(skia) || IsSymbolicLink(skia))
            throw new FileNotFoundException("Linux x64 libSkiaSharp.so is missing from the launcher payload.", skia);
    }

    private static void RefuseSymbolicLink(string path, string description)
    {
        if (IsSymbolicLink(path))
            throw new InvalidOperationException($"Refusing to use a symbolic link as the {description}: {path}");
    }

    private static void DeleteDirectoryTreeNoFollow(string path)
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
