using Avalonia;
using OSFR.Linux.Installer.Services;

namespace OSFR.Linux.Installer;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--diagnose", StringComparer.OrdinalIgnoreCase))
        {
            PrintDiagnostics();
            return;
        }

        if (args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase))
        {
            PrintDryRun();
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static void PrintDiagnostics()
    {
        var state = SystemDetector.Detect();
        Console.WriteLine("Sanctuary Linux Installer diagnostics");
        Console.WriteLine($"Operating system: {state.OsName}");
        Console.WriteLine($"Kernel: {state.KernelVersion}");
        Console.WriteLine($"Desktop: {state.Desktop}");
        Console.WriteLine($"Session: {state.SessionType}");
        Console.WriteLine($"CPU: {state.CpuModel}");
        Console.WriteLine($"Architecture: {(state.IsX64 ? "x86_64" : "unsupported / non-x86_64")}");
        Console.WriteLine($"Memory: {state.Memory}");
        Console.WriteLine($"GPU: {state.Gpu}");
        Console.WriteLine($"Steam: {state.SteamRoot ?? "not found"}");
        Console.WriteLine($"Recommended Proton: {state.ProtonPath ?? "not found"}");
        Console.WriteLine("Detected Proton builds:");
        foreach (var proton in state.ProtonCandidates ?? [])
            Console.WriteLine($"  - {proton.Name}: {proton.Path}{(proton.Recommended ? " [recommended]" : string.Empty)}");
        Console.WriteLine($"Default install path: {InstallService.DefaultInstallRoot}");
        Console.WriteLine($"Installer log: {InstallerLog.LogPath}");
    }

    private static void PrintDryRun()
    {
        var state = SystemDetector.Detect();
        Console.WriteLine("Sanctuary Linux Installer dry run - no files will be changed");
        Console.WriteLine($"Detected OS: {state.OsName}");
        Console.WriteLine($"Detected CPU: {state.CpuModel}");
        Console.WriteLine($"Detected memory: {state.Memory}");
        Console.WriteLine($"Detected GPU: {state.Gpu}");
        Console.WriteLine($"Would install to: {InstallService.DefaultInstallRoot}");
        Console.WriteLine($"Would use Steam: {state.SteamRoot ?? "not found"}");
        Console.WriteLine($"Would use Proton: {state.ProtonPath ?? "not found"}");
        Console.WriteLine($"Would create dedicated prefix: {Path.Combine(InstallService.DefaultInstallRoot, "ProtonPrefix")}");
        Console.WriteLine($"Would create launcher data under: {InstallService.LauncherDataRoot}");
        Console.WriteLine("Would create application-menu integration and, when available, a Desktop shortcut.");
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
