using System.Runtime.InteropServices;

namespace Launcher.Helpers;

public static partial class Dx9Helper
{
    public static bool IsInstalled()
    {
        // Linux: Proton provides the Windows DirectX compatibility
        // layer through Wine + DXVK/vkd3d. Native DirectX is not
        // installed on the Linux host.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return WineHelper.IsInstalled();

        // macOS: the Windows compatibility layer is responsible for
        // providing the game's DirectX environment.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return WineHelper.IsInstalled();

        // Windows: DirectX 9 is supplied by Windows.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        return true;
    }
}
