using System.Reflection;
using OSFR.Linux.Installer.Services;

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
var normalized = InstallService.NormalizeInstallRoot("~/OSFR-Smoke-Test");
Assert(normalized == Path.GetFullPath(Path.Combine(home, "OSFR-Smoke-Test")), "Tilde install paths must resolve under the user home directory.");
Assert(InstallService.GetInstallDestinationError(home) is not null, "The home directory must not be accepted as an install root.");
Assert(InstallService.GetInstallDestinationError(Path.GetPathRoot(home)!) is not null, "The filesystem root must not be accepted as an install root.");
Assert(InstallService.IsPathInside(InstallService.DesktopIconPath, home), "The installed application icon must stay inside the user home directory.");

Assert(InstallService.IsSafeArchiveEntry("OSFRLauncher"), "Normal archive entries must be accepted.");
Assert(InstallService.IsSafeArchiveEntry("runtimes/linux-x64/native/libSkiaSharp.so"), "Nested archive entries must be accepted.");
Assert(!InstallService.IsSafeArchiveEntry("../escape"), "Parent traversal must be rejected.");
Assert(!InstallService.IsSafeArchiveEntry("folder/../../escape"), "Nested parent traversal must be rejected.");
Assert(!InstallService.IsSafeArchiveEntry("/tmp/escape"), "Absolute archive paths must be rejected.");
Assert(!InstallService.IsSafeArchiveEntry("\\tmp\\escape"), "Rooted backslash paths must be rejected.");

Assert(InstallService.IsPathInside(Path.Combine(home, ".cache", "OSFR-Linux"), home), "Known OSFR data inside home must pass the home boundary check.");
Assert(!InstallService.IsPathInside(Path.GetTempPath(), home), "Paths outside home must fail the home boundary check.");

var modernVdf = """
                "libraryfolders"
                {
                    "0" { "path" "/home/test/.local/share/Steam" }
                    "1" { "path" "/mnt/games/SteamLibrary" }
                }
                """;
var parsedLibraries = SystemDetector.ParseSteamLibraryPaths(modernVdf).ToList();
Assert(parsedLibraries.Count == 2, "Steam VDF parsing must find nested library path entries.");
Assert(parsedLibraries[1] == "/mnt/games/SteamLibrary", "Steam VDF parsing must preserve custom library paths.");

var legacyVdf = "\"1\" \"/mnt/legacy\"\n\"path\" \"/mnt/current\"";
Assert(SystemDetector.ParseSteamLibraryPaths(legacyVdf).Single() == "/mnt/current", "Steam path parsing must ignore unrelated legacy numeric entries safely.");

var ready = new SystemState(true, true, "/tmp/fake-steam", "/tmp/fake-proton");
Assert(ready.Ready, "A complete Linux x64 Steam/Proton state should be ready.");
var notReady = new SystemState(true, true, null, "/tmp/fake-proton");
Assert(!notReady.Ready, "Steam is required for readiness.");

var protonLibrary = Path.Combine(Path.GetTempPath(), $"osfr-proton-ranking-{Guid.NewGuid():N}");
try
{
    foreach (var name in new[] { "Proton 9.0", "Proton 10.0", "Proton Experimental", "Custom-Proton-Build" })
    {
        var dir = Path.Combine(protonLibrary, "steamapps", "common", name);
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "proton"), string.Empty);
    }

    var toolsDir = Path.Combine(protonLibrary, "compatibilitytools.d", "GE-Proton10-30");
    Directory.CreateDirectory(toolsDir);
    await File.WriteAllTextAsync(Path.Combine(toolsDir, "proton"), string.Empty);

    var candidates = SystemDetector.FindProtonCandidates([protonLibrary]);
    Assert(candidates.Count >= 5, "Proton discovery must find standard, custom-named, and compatibility-tool builds.");
    Assert(candidates[0].Name.Contains("Experimental", StringComparison.OrdinalIgnoreCase), "Proton Experimental must remain the default recommendation when installed.");
    Assert(candidates.Any(p => p.Name == "Proton 10.0"), "Proton 10.0 must be exposed as a selectable candidate.");
    Assert(candidates.Any(p => p.Name == "Custom-Proton-Build"), "Custom-named Steam compatibility tools with a proton launcher must be exposed.");
    Assert(candidates.Any(p => p.Name == "GE-Proton10-30"), "GE-Proton compatibility tools must be exposed.");
}
finally
{
    if (Directory.Exists(protonLibrary))
        Directory.Delete(protonLibrary, true);
}

var service = new InstallService();
var tempRoot = Path.Combine(Path.GetTempPath(), $"osfr-installer-smoke-{Guid.NewGuid():N}");
Directory.CreateDirectory(tempRoot);
var sentinel = Path.Combine(tempRoot, "do-not-delete.txt");
await File.WriteAllTextAsync(sentinel, "unrelated user data");

try
{
    Assert(InstallService.GetInstallDestinationError(tempRoot) is not null, "Live validation must reject a non-empty unrelated directory.");
    Assert(!service.IsInstalled(tempRoot), "An unrelated directory must never be reported as an OSFR installation.");

    var installRejected = false;
    try { await service.InstallAsync(tempRoot, ready, new Progress<InstallProgress>()); }
    catch (InvalidOperationException) { installRejected = true; }
    Assert(installRejected, "Install must reject a non-empty unrelated directory.");
    Assert(File.Exists(sentinel), "Rejected install destinations must remain untouched.");

    var uninstallRejected = false;
    try { await service.UninstallAsync(tempRoot, new Progress<InstallProgress>()); }
    catch (InvalidOperationException) { uninstallRejected = true; }
    Assert(uninstallRejected, "Uninstall must reject a directory it does not own.");
    Assert(File.Exists(sentinel), "Uninstall must never delete unowned user data.");
}
finally
{
    if (Directory.Exists(tempRoot))
        Directory.Delete(tempRoot, true);
}

if (OperatingSystem.IsLinux())
{
    var symlinkTestRoot = Path.Combine(Path.GetTempPath(), $"osfr-symlink-smoke-{Guid.NewGuid():N}");
    var external = Path.Combine(Path.GetTempPath(), $"osfr-external-{Guid.NewGuid():N}");
    var link = Path.Combine(symlinkTestRoot, "linked-install");
    Directory.CreateDirectory(symlinkTestRoot);
    Directory.CreateDirectory(external);
    var outsideSentinel = Path.Combine(external, "must-survive.txt");
    await File.WriteAllTextAsync(outsideSentinel, "safe");

    try
    {
        Directory.CreateSymbolicLink(link, external);
        Assert(InstallService.IsSymbolicLink(link), "Installer must recognize directory symbolic links.");
        Assert(InstallService.GetInstallDestinationError(link) is not null, "Symbolic links must not be accepted as install roots.");
        Assert(File.Exists(outsideSentinel), "Symlink validation must not alter the target directory.");
    }
    finally
    {
        if (Directory.Exists(link) || InstallService.IsSymbolicLink(link))
            Directory.Delete(link, false);
        if (Directory.Exists(symlinkTestRoot))
            Directory.Delete(symlinkTestRoot, true);
        if (Directory.Exists(external))
            Directory.Delete(external, true);
    }
}

if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
{
    var createDesktopEntries = typeof(InstallService).GetMethod("CreateDesktopEntries", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not locate desktop-entry generator for validation.");

    createDesktopEntries.Invoke(null, ["/tmp/OSFR Test/OSFRLauncher"]);
    var generatedDesktop = Path.Combine(home, ".local", "share", "applications", "OSFR-Linux.desktop");
    Assert(File.Exists(generatedDesktop), "CI desktop-entry generation must produce an application-menu entry.");

    var desktopText = await File.ReadAllTextAsync(generatedDesktop);
    Assert(desktopText.Contains("Icon=osfr-linux", StringComparison.Ordinal), "Desktop entry must use the stable icon-theme name.");
    Assert(desktopText.Contains("StartupWMClass=OSFRLauncher", StringComparison.Ordinal), "Desktop entry must declare the launcher window class for taskbar grouping.");
    Console.WriteLine($"Generated desktop entry: {generatedDesktop}");
}

Console.WriteLine("Installer safety smoke tests passed.");
