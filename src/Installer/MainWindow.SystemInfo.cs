using Avalonia.Interactivity;

namespace OSFR.Linux.Installer;

public partial class MainWindow
{
    private void HardwareStatusOpened(object? sender, EventArgs e)
    {
        RefreshHardwareCompatibilityUi();
    }

    private void RefreshHardwareCompatibilityUi()
    {
        var desktopDetected = !string.IsNullOrWhiteSpace(_host.Desktop) &&
                              !string.Equals(_host.Desktop, "unknown", StringComparison.OrdinalIgnoreCase);
        DesktopStatus.Text = desktopDetected
            ? $"✓ {_host.Desktop} ({_host.Session})"
            : "✗ Desktop environment not detected";
        DesktopStatus.Foreground = desktopDetected ? Good : Bad;

        var gpuDetected = !string.IsNullOrWhiteSpace(_host.Gpu) &&
                          !string.Equals(_host.Gpu, "not detected", StringComparison.OrdinalIgnoreCase);
        GpuStatus.Text = gpuDetected
            ? $"✓ {_host.Gpu}"
            : "✗ GPU not detected";
        GpuStatus.Foreground = gpuDetected ? Good : Bad;

        // Keep the welcome page intentionally clean: detection status only, no filesystem paths.
        SteamStatus.Text = _state.SteamRoot is not null ? "✓ DETECTED" : "✗ NOT FOUND";
        SteamStatus.Foreground = _state.SteamRoot is not null ? Good : Bad;

        ProtonStatus.Text = _state.ProtonCompatible ? "✓ DETECTED" : "✗ NOT FOUND / INCOMPATIBLE";
        ProtonStatus.Foreground = _state.ProtonCompatible ? Good : Bad;
    }
}
