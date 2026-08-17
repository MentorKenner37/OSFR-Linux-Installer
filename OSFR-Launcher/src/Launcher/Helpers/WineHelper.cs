using System;
using System.IO;

namespace Launcher.Helpers;

public static class WineHelper
{
    public static string GetPath()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string[] candidates =
        [
            // Steam Debian installation
            Path.Combine(home, ".steam/debian-installation/steamapps/common/Proton - Experimental/proton"),
            Path.Combine(home, ".steam/debian-installation/steamapps/common/Proton Hotfix/proton"),

            // Standard Steam paths
            Path.Combine(home, ".local/share/Steam/steamapps/common/Proton - Experimental/proton"),
            Path.Combine(home, ".local/share/Steam/steamapps/common/Proton Hotfix/proton"),
            Path.Combine(home, ".steam/steam/steamapps/common/Proton - Experimental/proton"),
            Path.Combine(home, ".steam/steam/steamapps/common/Proton Hotfix/proton"),

            // Manually installed Proton
            Path.Combine(home, "Proton/GE-Proton10-30/proton"),
            Path.Combine(home, "Proton/GE-Proton10-15/proton")
        ];

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        return string.Empty;
    }

    public static bool IsInstalled()
    {
        return !string.IsNullOrEmpty(GetPath());
    }
}
