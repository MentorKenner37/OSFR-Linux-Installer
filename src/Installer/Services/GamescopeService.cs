using System.Diagnostics;
using System.Text.Json;

namespace OSFR.Linux.Installer.Services;

internal sealed record GamescopeInstallCommand(string Manager, string Executable, IReadOnlyList<string> Arguments);
internal sealed record GamescopeInstallReceipt(string Manager, bool AddedDebianBackports);

internal static class GamescopeService
{
    private const string OwnershipFileName = ".gamescope-installed-by-sanctuary.json";
    private const string DebianBackportsFile = "/etc/apt/sources.list.d/sanctuary-trixie-backports.list";
    private const string DebianBackportsLine = "deb http://deb.debian.org/debian trixie-backports main contrib non-free-firmware";

    private static readonly string[] CommonPaths =
    [
        "/usr/bin/gamescope",
        "/usr/games/gamescope",
        "/usr/local/bin/gamescope"
    ];

    public static string? FindInstalledPath()
    {
        foreach (var path in CommonPaths)
            if (File.Exists(path))
                return path;

        return FindOnPath("gamescope");
    }

    public static GamescopeInstallCommand? GetInstallCommand()
    {
        if (FindOnPath("pkexec") is not { } pkexec)
            return null;

        if (FindOnPath("dnf5") is not null)
            return new("DNF5", pkexec, ["dnf5", "install", "-y", "gamescope"]);
        if (FindOnPath("dnf") is not null)
            return new("DNF", pkexec, ["dnf", "install", "-y", "gamescope"]);
        if (FindOnPath("apt-get") is not null)
            return new(IsDebianTrixie() ? "APT (official Debian backports)" : "APT", pkexec, ["apt-get", "install", "-y", "gamescope"]);
        if (FindOnPath("pacman") is not null)
            return new("Pacman", pkexec, ["pacman", "-S", "--needed", "--noconfirm", "gamescope"]);
        if (FindOnPath("zypper") is not null)
            return new("Zypper", pkexec, ["zypper", "--non-interactive", "install", "gamescope"]);

        return null;
    }

    public static async Task<GamescopeInstallReceipt?> InstallAsync(CancellationToken cancellationToken = default)
    {
        if (FindInstalledPath() is not null)
            return null;

        var command = GetInstallCommand() ?? throw new InvalidOperationException(
            "Automatic Gamescope installation is unavailable because a supported package manager and pkexec were not found. Install the 'gamescope' package manually, then restart the installer.");

        var addedBackports = false;
        if (command.Manager.StartsWith("APT", StringComparison.Ordinal) && IsDebianTrixie())
        {
            addedBackports = !HasAptCandidate();
            if (addedBackports)
            {
                InstallerLog.Info("The Gamescope package is unavailable in the configured APT sources; official Debian 13 backports will be enabled.");
                command = command with
                {
                    Arguments = ["/bin/sh", "-c",
                        $"printf '%s\\n' '{DebianBackportsLine}' > '{DebianBackportsFile}' && /usr/bin/apt-get update && /usr/bin/apt-get install -y -t trixie-backports gamescope"]
                };
            }
            else
            {
                command = command with
                {
                    Arguments = ["/bin/sh", "-c",
                        "/usr/bin/apt-get update && /usr/bin/apt-get install -y -t trixie-backports gamescope"]
                };
            }
        }

        InstallerLog.Info($"Requesting Gamescope installation through {command.Manager}.");
        try
        {
            await RunElevatedAsync(command.Manager, command.Executable, command.Arguments, cancellationToken);
        }
        catch
        {
            if (addedBackports && File.Exists(DebianBackportsFile))
            {
                try { await RunElevatedAsync("failed Gamescope install cleanup", command.Executable, ["/usr/bin/rm", "--", DebianBackportsFile], cancellationToken); }
                catch (Exception cleanupEx) { InstallerLog.Warn($"Could not remove the temporary Debian backports source: {cleanupEx.Message}"); }
            }
            throw;
        }

        if (FindInstalledPath() is null)
            throw new InvalidOperationException("The package manager completed, but the Gamescope executable could not be found.");

        InstallerLog.Info("Gamescope installed and verified successfully.");
        return new GamescopeInstallReceipt(command.Manager, addedBackports);
    }

    public static void RecordInstallerOwnership(string installRoot, GamescopeInstallReceipt receipt)
    {
        var marker = Path.Combine(InstallService.NormalizeInstallRoot(installRoot), OwnershipFileName);
        SafeFileSystem.RefuseSymbolicLink(marker, "Gamescope ownership marker");
        File.WriteAllText(marker, JsonSerializer.Serialize(receipt));
    }

    public static bool IsInstallerOwned(string installRoot) => ReadOwnership(installRoot) is not null;

    public static async Task UninstallOwnedAsync(string installRoot, CancellationToken cancellationToken = default)
    {
        var receipt = ReadOwnership(installRoot)
            ?? throw new InvalidOperationException("Sanctuary did not install this Gamescope package, so it will not be removed automatically.");
        var pkexec = FindOnPath("pkexec")
            ?? throw new InvalidOperationException("pkexec is required to uninstall the system Gamescope package.");

        IReadOnlyList<string> arguments = receipt.Manager switch
        {
            var manager when manager.StartsWith("APT", StringComparison.Ordinal) => ["apt-get", "remove", "-y", "gamescope"],
            "DNF5" => ["dnf5", "remove", "-y", "gamescope"],
            "DNF" => ["dnf", "remove", "-y", "gamescope"],
            "Pacman" => ["pacman", "-R", "--noconfirm", "gamescope"],
            "Zypper" => ["zypper", "--non-interactive", "remove", "gamescope"],
            _ => throw new InvalidOperationException("The recorded Gamescope package manager is not supported for automatic removal.")
        };

        await RunElevatedAsync($"{receipt.Manager} Gamescope removal", pkexec, arguments, cancellationToken);
        if (FindInstalledPath() is not null)
            throw new InvalidOperationException("The package manager completed, but Gamescope still appears to be installed.");

        if (receipt.AddedDebianBackports && File.Exists(DebianBackportsFile))
            await RunElevatedAsync("Sanctuary Debian backports cleanup", pkexec, ["/usr/bin/rm", "--", DebianBackportsFile], cancellationToken);

        InstallerLog.Info("Installer-owned Gamescope package removed successfully.");
    }

    private static GamescopeInstallReceipt? ReadOwnership(string installRoot)
    {
        try
        {
            var marker = Path.Combine(InstallService.NormalizeInstallRoot(installRoot), OwnershipFileName);
            if (!File.Exists(marker) || SafeFileSystem.IsSymbolicLink(marker))
                return null;
            return JsonSerializer.Deserialize<GamescopeInstallReceipt>(File.ReadAllText(marker));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            InstallerLog.Warn($"Could not read Gamescope ownership metadata: {ex.Message}");
            return null;
        }
    }

    private static async Task RunElevatedAsync(string operation, string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {operation}.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = (await outputTask).Trim();
        var error = (await errorTask).Trim();

        if (!string.IsNullOrWhiteSpace(output))
            InstallerLog.Info($"{operation} output: {output}");
        if (!string.IsNullOrWhiteSpace(error))
            InstallerLog.Warn($"{operation} error output: {error}");
        if (process.ExitCode != 0)
        {
            var detail = !string.IsNullOrWhiteSpace(error) ? error : output;
            throw new InvalidOperationException($"{operation} failed (exit code {process.ExitCode}).{(string.IsNullOrWhiteSpace(detail) ? " The administrator prompt may have been cancelled." : $" {detail}")}");
        }
    }

    private static bool IsDebianTrixie()
    {
        try
        {
            var values = File.ReadAllLines("/etc/os-release");
            return values.Any(line => line.Equals("ID=debian", StringComparison.OrdinalIgnoreCase))
                   && values.Any(line => line.Equals("VERSION_CODENAME=trixie", StringComparison.OrdinalIgnoreCase));
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static bool HasAptCandidate()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "/usr/bin/apt-cache",
                Arguments = "policy gamescope",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = "/"
            });
            if (process is null)
                return false;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            return process.ExitCode == 0
                   && output.Split('\n').Any(line => line.TrimStart().StartsWith("Candidate:", StringComparison.Ordinal)
                       && !line.Contains("(none)", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or System.ComponentModel.Win32Exception)
        {
            InstallerLog.Warn($"Could not query the APT Gamescope candidate: {ex.Message}");
            return false;
        }
    }

    private static string? FindOnPath(string executable)
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory))
                continue;
            var candidate = Path.Combine(directory, executable);
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }
}
