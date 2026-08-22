using OSFR.Linux.Installer.Services;

namespace OSFR.Linux.Installer;

public partial class MainWindow
{
    public void ApplyCompatibilityAdvisor()
    {
        var preferred = CompatibilityAdvisor.SelectPreferredProton(_state.ProtonCandidates ?? []);
        if (preferred is not null && ProtonComboBox.SelectedItem is ProtonCandidate selected && selected.Path != preferred.Path)
            ProtonComboBox.SelectedItem = preferred;

        var compatibility = CompatibilityAdvisor.Detect(_state);
        GraphicsBackendComboBox.SelectedIndex = compatibility.RecommendedGraphicsBackend == GraphicsBackendConfig.WineD3D ? 1 : 0;

        // The original XAML did not wire the graphics SelectionChanged handler, so do it here.
        // This keeps the review summary in sync when the user overrides the automatic recommendation.
        GraphicsBackendComboBox.SelectionChanged += GraphicsBackendSelectionChanged;
        ProtonComboBox.SelectionChanged += (_, _) => RefreshCompatibilityDetails();

        RefreshSummary();
        RefreshCompatibilityDetails();
    }

    private void RefreshCompatibilityDetails()
    {
        var compatibility = CompatibilityAdvisor.Detect(_state);
        var selectedProton = ProtonComboBox.SelectedItem as ProtonCandidate;

        SetCheck(
            SteamStatus,
            _state.SteamRoot is not null,
            _state.SteamRoot is null ? "NOT FOUND" : compatibility.SteamInstallType);

        SetCheck(
            ProtonStatus,
            selectedProton?.Compatible == true,
            selectedProton?.Name ?? "NOT FOUND / INCOMPATIBLE");

        var lines = new List<string>
        {
            $"OS: {_state.OsName}",
            $"Kernel: {_state.KernelVersion}",
            $"Desktop: {_state.Desktop} ({_state.SessionType})",
            $"CPU: {_state.CpuModel}",
            $"Memory: {_state.Memory}",
            $"GPU: {_state.Gpu}",
            $"Steam: {compatibility.SteamInstallType} — {_state.SteamRoot ?? "not found"}",
            $"Proton: {selectedProton?.Name ?? "not found"}",
            $"32-bit FreeType: {CompatibilityAdvisor.ProbeLabel(compatibility.FreeType32)}",
            $"32-bit OpenGL: {CompatibilityAdvisor.ProbeLabel(compatibility.OpenGl32)}",
            $"64-bit Vulkan loader: {CompatibilityAdvisor.ProbeLabel(compatibility.Vulkan64)}",
            $"32-bit Vulkan loader: {CompatibilityAdvisor.ProbeLabel(compatibility.Vulkan32)}",
            $"Recommended graphics backend: {GraphicsBackendConfig.DisplayName(compatibility.RecommendedGraphicsBackend)}",
            compatibility.GraphicsRecommendationReason
        };

        foreach (var warning in compatibility.Warnings)
            lines.Add($"WARNING: {warning}");

        if (!string.IsNullOrWhiteSpace(compatibility.PackageGuidance))
            lines.Add($"Package guidance: {compatibility.PackageGuidance}");

        DetailText.Text = string.Join(Environment.NewLine, lines);
    }
}
