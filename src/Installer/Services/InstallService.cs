using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;

namespace OSFR.Linux.Installer.Services;

public sealed record InstallProgress(int Percent, string Message);

public sealed class InstallService
{
    private const string PayloadResource = "OSFR.Linux.Installer.Payload";

    public static string DefaultInstallRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".local", "share", "OSFR-Linux");

    public static string LauncherDataRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".local", "share", "OSFRLauncher");

    public bool IsInstalled(string installRoot) =>
        File.Exists(Path.Combine(installRoot, "Launcher", "OSFRLauncher"));

    public async Task InstallAsync(
        string installRoot,
        SystemState state,
        IProgress<InstallProgress> progress,
        CancellationToken cancellationToken = default)
    {
        if (!state.Ready || state.SteamRoot is null || state.ProtonPath is null)
            throw new InvalidOperationException("Linux, x86_64, Steam and Proton are required.");

        installRoot = Path.GetFullPath(Environment.ExpandEnvironmentVariables(installRoot));
        var launcherDir = Path.Combine(installRoot, "Launcher");
        var prefixDir = Path.Combine(installRoot, "ProtonPrefix");

        progress.Report(new(5, "Preparing installation..."));
        Directory.CreateDirectory(installRoot);
        Directory.CreateDirectory(prefixDir);

        if (Directory.Exists(launcherDir))
            Directory.Delete(launcherDir, true);
        Directory.CreateDirectory(launcherDir);

        progress.Report(new(15, "Extracting OSFR Launcher..."));
        await ExtractLauncherPayloadAsync(launcherDir, cancellationToken);

        var launcher = Path.Combine(launcherDir, "OSFRLauncher");
        if (!File.Exists(launcher))
            throw new InvalidDataException("The embedded launcher payload did not contain OSFRLauncher.");

        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(
                    launcher,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
            catch { }
        }

        progress.Report(new(55, "Configuring Steam Proton..."));
        await File.WriteAllTextAsync(Path.Combine(launcherDir, "proton-path.txt"), state.ProtonPath + Environment.NewLine, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(launcherDir, "steam-path.txt"), state.SteamRoot + Environment.NewLine, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(launcherDir, "prefix-path.txt"), prefixDir + Environment.NewLine, cancellationToken);

        progress.Report(new(70, "Creating desktop integration..."));
        var iconPath = await ExtractIconAsync(launcherDir, cancellationToken);
        CreateDesktopEntries(launcher, iconPath);

        progress.Report(new(85, "Verifying installation..."));
        VerifyLauncher(launcherDir, launcher);

        var info = $"""
                   OSFR Linux Installation

                   Launcher: {launcher}
                   Steam: {state.SteamRoot}
                   Proton: {state.ProtonPath}
                   Proton Prefix: {prefixDir}
                   """;
        await File.WriteAllTextAsync(Path.Combine(installRoot, "install-info.txt"), info, cancellationToken);

        progress.Report(new(100, "Installation complete"));
    }

    public Task UninstallAsync(
        string installRoot,
        IProgress<InstallProgress> progress,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            progress.Report(new(10, "Stopping OSFR processes..."));
            StopProcesses();

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var targets = new[]
            {
                installRoot,
                LauncherDataRoot,
                Path.Combine(home, ".cache", "OSFRLauncher"),
                Path.Combine(home, ".cache", "OSFR-Linux"),
                Path.Combine(home, ".local", "share", "applications", "OSFR-Linux.desktop"),
                Path.Combine(home, "Desktop", "OSFR-Linux.desktop")
            };

            for (var i = 0; i < targets.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var target = targets[i];
                var percent = 20 + (int)(70.0 * (i + 1) / targets.Length);
                progress.Report(new(percent, $"Removing {Path.GetFileName(target)}..."));

                try
                {
                    if (Directory.Exists(target))
                        Directory.Delete(target, true);
                    else if (File.Exists(target))
                        File.Delete(target);
                }
                catch (DirectoryNotFoundException) { }
                catch (FileNotFoundException) { }
            }

            progress.Report(new(100, "Uninstallation complete"));
        }, cancellationToken);
    }

    public void Launch(string installRoot)
    {
        var launcher = Path.Combine(installRoot, "Launcher", "OSFRLauncher");
        if (!File.Exists(launcher))
            throw new FileNotFoundException("OSFRLauncher was not found.", launcher);

        Process.Start(new ProcessStartInfo
        {
            FileName = launcher,
            WorkingDirectory = Path.GetDirectoryName(launcher)!,
            UseShellExecute = false
        });
    }

    private static async Task ExtractLauncherPayloadAsync(string launcherDir, CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetExecutingAssembly();
        await using var payload = assembly.GetManifestResourceStream(PayloadResource)
            ?? throw new InvalidOperationException(
                "This installer build does not contain the launcher payload. Download a packaged GitHub Release build.");

        using var archive = new ZipArchive(payload, ZipArchiveMode.Read, leaveOpen: false);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destination = Path.GetFullPath(Path.Combine(launcherDir, entry.FullName));
            var root = Path.GetFullPath(launcherDir) + Path.DirectorySeparatorChar;
            if (!destination.StartsWith(root, StringComparison.Ordinal))
                throw new InvalidDataException("Invalid path in launcher payload.");

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using var source = entry.Open();
            await using var output = File.Create(destination);
            await source.CopyToAsync(output, cancellationToken);
        }
    }

    private static async Task<string> ExtractIconAsync(string launcherDir, CancellationToken cancellationToken)
    {
        var iconPath = Path.Combine(launcherDir, "OSFRLauncher.png");
        var assembly = Assembly.GetExecutingAssembly();
        await using var stream = assembly.GetManifestResourceStream("OSFR.Linux.Installer.Icon");
        if (stream is null)
            return iconPath;

        await using var output = File.Create(iconPath);
        await stream.CopyToAsync(output, cancellationToken);
        return iconPath;
    }

    private static void CreateDesktopEntries(string launcher, string iconPath)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var applications = Path.Combine(home, ".local", "share", "applications");
        Directory.CreateDirectory(applications);

        var desktopEntry = $"""
                           [Desktop Entry]
                           Type=Application
                           Name=Open Source Free Realms
                           Comment=Open Source Free Realms - Linux
                           Exec={EscapeDesktopValue(launcher)}
                           Icon={EscapeDesktopValue(iconPath)}
                           Terminal=false
                           Categories=Game;
                           StartupNotify=true
                           """;

        var applicationFile = Path.Combine(applications, "OSFR-Linux.desktop");
        File.WriteAllText(applicationFile, desktopEntry);

        var desktop = Path.Combine(home, "Desktop");
        if (Directory.Exists(desktop))
            File.WriteAllText(Path.Combine(desktop, "OSFR-Linux.desktop"), desktopEntry);
    }

    private static string EscapeDesktopValue(string value) => value.Replace("\\", "\\\\").Replace(" ", "\\ ");

    private static void VerifyLauncher(string launcherDir, string launcher)
    {
        if (!File.Exists(launcher))
            throw new FileNotFoundException("Launcher verification failed.", launcher);

        var skia = Path.Combine(launcherDir, "libSkiaSharp.so");
        if (!File.Exists(skia))
            throw new FileNotFoundException("Linux x64 libSkiaSharp.so is missing from the launcher payload.", skia);
    }

    private static void StopProcesses()
    {
        foreach (var name in new[] { "OSFRLauncher", "FreeRealms", "FreeRealms.exe" })
        {
            try
            {
                foreach (var process in Process.GetProcessesByName(name))
                {
                    try { process.Kill(entireProcessTree: true); }
                    catch { }
                    finally { process.Dispose(); }
                }
            }
            catch { }
        }
    }
}
