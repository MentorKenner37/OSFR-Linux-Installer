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
        var compatibility = CompatibilityAdvisor.Detect(state);
        Console.WriteLine("Sanctuary Linux Installer diagnostics");
        Console.WriteLine($"Operating system: {state.OsName}");
        Console.WriteLine($"Kernel: {state.KernelVersion}");
        Console.WriteLine($"Desktop: {state.Desktop}");
        Console.WriteLine($"Session: {state.SessionType}");
        Console.WriteLine($"CPU: {state.CpuModel}");
        Console.WriteLine($"Architecture: {(state.IsX64 ? "x86_64" : "unsupported / non-x86_64")}");
        Console.WriteLine($"Memory: {state.Memory}");
        Console.WriteLine($"GPU: {state.Gpu}");
        Console.WriteLine($"Steam type: {compatibility.SteamInstallType}");
        Console.WriteLine($"Steam: {state.SteamRoot ?? "not found"}");
        Console.WriteLine($"Recommended Proton: {state.ProtonPath ?? "not found"}");
        Console.WriteLine("Detected Proton builds:");
        foreach (var proton in state.ProtonCandidates ?? [])
            Console.WriteLine($"  - {proton.Name}: {proton.Path}{(proton.Recommended ? " [recommended]" : string.Empty)}");
        Console.WriteLine("Runtime/graphics checks:");
        Console.WriteLine($"  - 32-bit FreeType: {CompatibilityAdvisor.ProbeLabel(compatibility.FreeType32)}");
        Console.WriteLine($"  - 32-bit OpenGL: {CompatibilityAdvisor.ProbeLabel(compatibility.OpenGl32)}");
        Console.WriteLine($"  - 64-bit Vulkan loader: {CompatibilityAdvisor.ProbeLabel(compatibility.Vulkan64)}");
        Console.WriteLine($"  - 32-bit Vulkan loader: {CompatibilityAdvisor.ProbeLabel(compatibility.Vulkan32)}");
        Console.WriteLine($"Recommended graphics backend: {GraphicsBackendConfig.DisplayName(compatibility.RecommendedGraphicsBackend)}");
        Console.WriteLine($"Graphics recommendation: {compatibility.GraphicsRecommendationReason}");
        foreach (var warning in compatibility.Warnings)
            Console.WriteLine($"Warning: {warning}");
        if (!string.IsNullOrWhiteSpace(compatibility.PackageGuidance))
            Console.WriteLine($"Package guidance: {compatibility.PackageGuidance}");
        Console.WriteLine($"Default install path: {InstallService.DefaultInstallRoot}");
        Console.WriteLine($"Installer log: {InstallerLog.LogPath}");
    }

    private static void PrintDryRun()
    {
        var state = SystemDetector.Detect();
        var compatibility = CompatibilityAdvisor.Detect(state);
        Console.WriteLine("Sanctuary Linux Installer dry run - no files will be changed");
        Console.WriteLine($"Detected OS: {state.OsName}");
        Console.WriteLine($"Detected CPU: {state.CpuModel}");
        Console.WriteLine($"Detected memory: {state.Memory}");
        Console.WriteLine($"Detected GPU: {state.Gpu}");
        Console.WriteLine($"Detected Steam type: {compatibility.SteamInstallType}");
        Console.WriteLine($"Recommended graphics backend: {GraphicsBackendConfig.DisplayName(compatibility.RecommendedGraphicsBackend)}");
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
