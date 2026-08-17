using System;
using System.IO;
using NLog;

namespace Launcher.Helpers;

public static class ProtonHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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
        var configured = GetConfiguredPath("proton-path.txt");
        if (File.Exists(configured))
            return configured;

        if (!string.IsNullOrWhiteSpace(configured))
            Logger.Error("Configured Proton path no longer exists: {path}", configured);
        else
            Logger.Error("Sanctuary was launched without a configured Proton path. Re-run Sanctuary Linux Installer to repair the installation.");

        return string.Empty;
    }
}
