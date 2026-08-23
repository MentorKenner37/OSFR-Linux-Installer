using Avalonia;
using OSFR.Linux.Installer.Services;

namespace OSFR.Linux.Installer;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--auto-upgrade", StringComparer.OrdinalIgnoreCase))
        {
            RunAutoUpgradeAsync(args).GetAwaiter().GetResult();
            return;
        }

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

    private static async Task RunAutoUpgradeAsync(string[] args)
    {
        var rootIndex = Array.FindIndex(args, value => value.Equals("--install-root", StringComparison.OrdinalIgnoreCase));
        if (rootIndex < 0 || rootIndex + 1 >= args.Length)
            throw new ArgumentException("--auto-upgrade requires --install-root.");
        var installRoot = InstallService.NormalizeInstallRoot(args[rootIndex + 1]);

        var waitIndex = Array.FindIndex(args, value => value.Equals("--wait-pid", StringComparison.OrdinalIgnoreCase));
        if (waitIndex >= 0 && waitIndex + 1 < args.Length && int.TryParse(args[waitIndex + 1], out var pid))
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45));
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            catch (TimeoutException) { throw new InvalidOperationException("The running Sanctuary launcher did not close in time for its update."); }
        }

        var service = new InstallService();
        var installation = service.GetInstallationInfo(installRoot);
        if (installation.Condition != InstallationCondition.Installed)
            throw new InvalidOperationException("Automatic upgrade requires a verified existing Sanctuary installation.");

        var display = DisplayModeConfig.Read(installRoot);
        var graphics = GraphicsBackendConfig.Read(installRoot);
        var configuredProton = ReadConfiguredPath(installRoot, "proton-path.txt");
        var state = SystemDetector.Detect();
        var selected = state.ProtonCandidates?.FirstOrDefault(candidate => candidate.Compatible && candidate.Path == configuredProton)
                       ?? CompatibilityAdvisor.SelectPreferredProton(state.ProtonCandidates ?? []);
        if (selected is null)
            throw new InvalidOperationException("No compatible Proton build is available for the automatic upgrade.");
        state = state.WithProton(selected);

        Console.WriteLine($"Updating Sanctuary at {installRoot}...");
        var progress = new Progress<InstallProgress>(value => Console.WriteLine($"{value.Percent}% {value.Message}"));
        await service.InstallAsync(installRoot, state, progress, new InstallOptions(installation.HasDesktopShortcut, RepairExisting: true));
        GraphicsBackendConfig.Write(installRoot, graphics);
        DisplayModeConfig.Write(installRoot, display.Mode, display.Width, display.Height);
        Console.WriteLine("Sanctuary update completed successfully.");
    }

    private static string ReadConfiguredPath(string installRoot, string name)
    {
        var path = Path.Combine(installRoot, "Launcher", name);
        return File.Exists(path) && !InstallService.IsSymbolicLink(path) ? File.ReadAllText(path).Trim() : string.Empty;
    }

    private static SystemState DetectWithPreferredProton()
    {
        var state = SystemDetector.Detect();
        var preferred = CompatibilityAdvisor.SelectPreferredProton(state.ProtonCandidates ?? []);
        return preferred is null ? state : state.WithProton(preferred);
    }

    private static void PrintDiagnostics()
    {
        var state = DetectWithPreferredProton();
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
        Console.WriteLine($"curl fallback: {state.CurlPath ?? "not found (normal downloads still available)"}");
        Console.WriteLine("Detected Proton builds:");
        foreach (var proton in state.ProtonCandidates ?? [])
            Console.WriteLine($"  - {proton.Name}: {proton.Path}{(state.ProtonPath == proton.Path ? " [recommended]" : string.Empty)}");
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
        var state = DetectWithPreferredProton();
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
        Console.WriteLine($"Would use curl fallback: {state.CurlPath ?? "not found"}");
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
