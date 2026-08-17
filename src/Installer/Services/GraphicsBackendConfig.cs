namespace OSFR.Linux.Installer.Services;

internal static class GraphicsBackendConfig
{
    public const string Dxvk = "dxvk";
    public const string WineD3D = "wined3d";
    public const string FileName = "graphics-backend.txt";

    public static string DisplayName(string backend) =>
        string.Equals(backend, WineD3D, StringComparison.OrdinalIgnoreCase)
            ? "OpenGL (WineD3D)"
            : "Vulkan (DXVK)";

    public static void Write(string installRoot, string backend)
    {
        if (backend is not Dxvk and not WineD3D)
            throw new ArgumentOutOfRangeException(nameof(backend), "Unknown graphics backend.");

        var launcherDir = Path.Combine(InstallService.NormalizeInstallRoot(installRoot), "Launcher");
        var launcher = Path.Combine(launcherDir, "OSFRLauncher");
        if (!File.Exists(launcher))
            throw new FileNotFoundException("Sanctuary launcher was not found while saving the graphics backend.", launcher);

        SafeFileSystem.RefuseSymbolicLink(launcherDir, "launcher directory");
        var destination = Path.Combine(launcherDir, FileName);
        SafeFileSystem.RefuseSymbolicLink(destination, "graphics backend configuration");

        var temporary = Path.Combine(launcherDir, $".{FileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, backend + Environment.NewLine);
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }

        InstallerLog.Info($"Configured Proton graphics backend: {DisplayName(backend)} ({backend})");
    }
}
