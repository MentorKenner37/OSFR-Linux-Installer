using System.Diagnostics;

namespace OSFR.Linux.Installer.Services;

internal sealed record GamescopeInstallCommand(string Manager, string Executable, IReadOnlyList<string> Arguments);

internal static class GamescopeService
{
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
            return new("APT", pkexec, ["apt-get", "install", "-y", "gamescope"]);
        if (FindOnPath("pacman") is not null)
            return new("Pacman", pkexec, ["pacman", "-S", "--needed", "--noconfirm", "gamescope"]);
        if (FindOnPath("zypper") is not null)
            return new("Zypper", pkexec, ["zypper", "--non-interactive", "install", "gamescope"]);

        return null;
    }

    public static async Task InstallAsync(CancellationToken cancellationToken = default)
    {
        if (FindInstalledPath() is not null)
            return;

        var command = GetInstallCommand() ?? throw new InvalidOperationException(
            "Automatic Gamescope installation is unavailable because a supported package manager and pkexec were not found. Install the 'gamescope' package manually, then restart the installer.");

        var startInfo = new ProcessStartInfo(command.Executable) { UseShellExecute = false };
        foreach (var argument in command.Arguments)
            startInfo.ArgumentList.Add(argument);

        InstallerLog.Info($"Requesting Gamescope installation through {command.Manager}.");
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start the {command.Manager} Gamescope installer.");
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{command.Manager} did not install Gamescope (exit code {process.ExitCode}). The administrator prompt may have been cancelled.");
        if (FindInstalledPath() is null)
            throw new InvalidOperationException("The package manager completed, but the Gamescope executable could not be found.");

        InstallerLog.Info("Gamescope installed and verified successfully.");
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
