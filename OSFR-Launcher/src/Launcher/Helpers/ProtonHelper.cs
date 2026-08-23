using System;
using System.IO;
using NLog;

namespace Launcher.Helpers;

public static class ProtonHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private const string GraphicsBackendFileName = "graphics-backend.txt";
    private const string DisplayModeFileName = "display-mode.txt";

    public sealed record GameLaunchPlan(string FileName, string Arguments, string Mode, int Width, int Height);

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

    public static GameLaunchPlan CreateGameLaunchPlan(string protonPath, string executableName, string gameArguments)
    {
        var (mode, width, height) = ReadDisplayMode();
        if (mode == "fullscreen")
            Logger.Info("Launching Free Realms directly through Proton with its native fullscreen option.");
        else
            Logger.Info("Launching Free Realms directly through Proton and preserving its native window controls.");

        // Free Realms uses two command-line parsers: GNU-style switches followed by
        // legacy key=value launch tokens. Keep --fullscreen before the first legacy
        // token; the 2014 client may stop looking for switches after those tokens.
        var clientArguments = mode == "fullscreen"
            ? $"--fullscreen {gameArguments}"
            : gameArguments;

        return new(
            protonPath,
            $"run \"{executableName}\" {clientArguments}",
            mode,
            width,
            height);
    }

    private static (string Mode, int Width, int Height) ReadDisplayMode()
    {
        var configured = GetConfiguredPath(DisplayModeFileName);
        var parts = configured.Split(':');
        if (parts.Length == 3 && parts[0] is "fullscreen" or "windowed" &&
            int.TryParse(parts[1], out var width) && int.TryParse(parts[2], out var height) &&
            width is >= 640 and <= 16384 && height is >= 480 and <= 16384)
            return (parts[0], width, height);

        if (!string.IsNullOrWhiteSpace(configured))
            Logger.Warn("Invalid display mode configuration '{displayMode}'. Using fullscreen 1920x1080.", configured);
        return ("fullscreen", 1920, 1080);
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
