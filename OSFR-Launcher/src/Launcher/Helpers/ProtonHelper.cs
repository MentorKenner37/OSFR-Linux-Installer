using System;
using System.IO;
using NLog;

namespace Launcher.Helpers;

public static class ProtonHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private const string GraphicsBackendFileName = "graphics-backend.txt";

    public static string GetConfiguredPath(string fileName)
    {
        var file = Path.Combine(AppContext.BaseDirectory, fileName);
        if (!File.Exists(file))
            return string.Empty;

        try
        {
            var value = File.ReadAllText(file).Trim();
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
        }
        catch (IOException ex)
        {
            Logger.Warn(ex, "Could not read configured runtime path file {file}", file);
            return string.Empty;
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.Warn(ex, "Could not access configured runtime path file {file}", file);
            return string.Empty;
        }
    }

    public static string GetSteamRoot()
    {
        var configured = GetConfiguredPath("steam-path.txt");
        if (Directory.Exists(configured))
            return configured;

        if (!string.IsNullOrWhiteSpace(configured))
            Logger.Error("Configured Steam path no longer exists: {path}", configured);
        else
            Logger.Error("Sanctuary was launched without a configured Steam path. Re-run Sanctuary Linux Installer to repair the installation.");

        return string.Empty;
    }

    public static string GetPath()
    {
        ApplyConfiguredGraphicsBackend();

        var configured = GetConfiguredPath("proton-path.txt");
        if (File.Exists(configured))
            return configured;

        if (!string.IsNullOrWhiteSpace(configured))
            Logger.Error("Configured Proton path no longer exists: {path}", configured);
        else
            Logger.Error("Sanctuary was launched without a configured Proton path. Re-run Sanctuary Linux Installer to repair the installation.");

        return string.Empty;
    }

    private static void ApplyConfiguredGraphicsBackend()
    {
        var backend = GetConfiguredPath(GraphicsBackendFileName).ToLowerInvariant();
        if (backend == "wined3d")
        {
            Environment.SetEnvironmentVariable("PROTON_USE_WINED3D", "1");
            Logger.Info("Using OpenGL WineD3D graphics backend for Proton.");
            return;
        }

        if (!string.IsNullOrEmpty(backend) && backend != "dxvk")
            Logger.Warn("Unknown graphics backend '{backend}'. Falling back to DXVK/Vulkan.", backend);

        // Explicitly disable WineD3D so a parent-shell environment override cannot
        // silently replace the user's Sanctuary selection.
        Environment.SetEnvironmentVariable("PROTON_USE_WINED3D", "0");
        Logger.Info("Using Vulkan DXVK graphics backend for Proton.");
    }
}
