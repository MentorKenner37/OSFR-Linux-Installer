using System.Reflection;
using OSFR.Linux.Installer.Services;

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static async Task CreateFakeProtonAsync(string directory, ushort elfMachine)
{
    Directory.CreateDirectory(directory);
    var proton = Path.Combine(directory, "proton");
    await File.WriteAllTextAsync(proton, "#!/usr/bin/env python3\n");
    if (OperatingSystem.IsLinux())
    {
        File.SetUnixFileMode(
            proton,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }

    var wineDirectory = Path.Combine(directory, "files", "bin");
    Directory.CreateDirectory(wineDirectory);
    var header = new byte[20];
    header[0] = 0x7F;
    header[1] = (byte)'E';
    header[2] = (byte)'L';
    header[3] = (byte)'F';
    header[4] = 2;
    header[5] = 1;
    header[18] = (byte)(elfMachine & 0xFF);
    header[19] = (byte)(elfMachine >> 8);
    await File.WriteAllBytesAsync(Path.Combine(wineDirectory, "wine64"), header);
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

var ready = new SystemState(true, true, "/tmp/fake-steam", "/tmp/fake-proton", ProtonCompatible: true);
Assert(ready.Ready, "A complete Linux x64 Steam/compatible-Proton state should be ready.");
var notReady = new SystemState(true, true, null, "/tmp/fake-proton", ProtonCompatible: true);
Assert(!notReady.Ready, "Steam is required for readiness.");
var incompatible = new SystemState(true, true, "/tmp/fake-steam", "/tmp/fake-proton", ProtonCompatible: false);
Assert(!incompatible.Ready, "An incompatible Proton runtime must never make the installer ready.");

var protonLibrary = Path.Combine(Path.GetTempPath(), $"osfr-proton-ranking-{Guid.NewGuid():N}");
try
{
    foreach (var name in new[] { "Proton 9.0", "Proton 10.0", "Proton Experimental", "Custom-Proton-Build" })
    {
        var dir = Path.Combine(protonLibrary, "steamapps", "common", name);
        await CreateFakeProtonAsync(dir, 62);
    }

    var toolsDir = Path.Combine(protonLibrary, "compatibilitytools.d", "GE-Proton10-30");
    await CreateFakeProtonAsync(toolsDir, 62);

    var armToolsDir = Path.Combine(protonLibrary, "compatibilitytools.d", "GE-Proton11-5-aarch64");
    await CreateFakeProtonAsync(armToolsDir, 183);

    var candidates = SystemDetector.FindProtonCandidates([protonLibrary]);
    Assert(candidates.Count >= 6, "Proton discovery must find standard, custom-named, GE, and architecture-mismatched builds.");
    Assert(candidates.First(p => p.Recommended).Name.Contains("Experimental", StringComparison.OrdinalIgnoreCase), "Compatible Proton Experimental must remain the default recommendation when installed.");
    Assert(candidates.Any(p => p.Name == "Proton 10.0" && p.Compatible), "Proton 10.0 must be exposed as a compatible selectable candidate.");
    Assert(candidates.Any(p => p.Name == "Custom-Proton-Build" && p.Compatible), "Custom-named Steam compatibility tools with a valid x86_64 runtime must be exposed.");
    Assert(candidates.Any(p => p.Name == "GE-Proton10-30" && p.Compatible), "x86_64 GE-Proton compatibility tools must be exposed.");

    var armCandidate = candidates.Single(p => p.Name == "GE-Proton11-5-aarch64");
    if (System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.X64)
    {
        Assert(!armCandidate.Compatible, "ARM64 Proton must be rejected on an x86_64 host.");
        Assert(!armCandidate.Recommended, "An incompatible ARM64 Proton build must never be recommended on x86_64.");
    }
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
    Assert(!service.IsInstalled(tempRoot), "An unrelated directory must never be reported as a Sanctuary installation.");

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

var ownershipRoot = Path.Combine(Path.GetTempPath(), $"sanctuary-owned-{Guid.NewGuid():N}");
var copiedRoot = Path.Combine(Path.GetTempPath(), $"sanctuary-copied-marker-{Guid.NewGuid():N}");
try
{
    var launcherDirectory = Path.Combine(ownershipRoot, "Launcher");
    Directory.CreateDirectory(launcherDirectory);
    await File.WriteAllTextAsync(Path.Combine(launcherDirectory, "OSFRLauncher"), "launcher");
    await File.WriteAllTextAsync(Path.Combine(ownershipRoot, InstallationOwnership.LegacyInstallInfoFileName), "Sanctuary Linux Installation");
    InstallationOwnership.Write(ownershipRoot);
    Assert(service.IsInstalled(ownershipRoot), "A structured ownership marker bound to its canonical install root and launcher hash must be recognized.");

    await File.AppendAllTextAsync(Path.Combine(launcherDirectory, "OSFRLauncher"), "tampered");
    Assert(!service.IsInstalled(ownershipRoot), "Changing the owned launcher after the ownership marker is written must invalidate ownership verification.");
    await File.WriteAllTextAsync(Path.Combine(launcherDirectory, "OSFRLauncher"), "launcher");
    InstallationOwnership.Write(ownershipRoot);

    var copiedLauncherDirectory = Path.Combine(copiedRoot, "Launcher");
    Directory.CreateDirectory(copiedLauncherDirectory);
    await File.WriteAllTextAsync(Path.Combine(copiedLauncherDirectory, "OSFRLauncher"), "launcher");
    File.Copy(
        Path.Combine(ownershipRoot, InstallationOwnership.MarkerFileName),
        Path.Combine(copiedRoot, InstallationOwnership.MarkerFileName));
    Assert(!service.IsInstalled(copiedRoot), "Copying an ownership marker to another directory must not authorize recursive uninstall there.");
}
finally
{
    if (Directory.Exists(ownershipRoot))
        Directory.Delete(ownershipRoot, true);
    if (Directory.Exists(copiedRoot))
        Directory.Delete(copiedRoot, true);
}

var transactionRoot = Path.Combine(Path.GetTempPath(), $"sanctuary-transaction-{Guid.NewGuid():N}");
try
{
    var oldLauncherDir = Path.Combine(transactionRoot, "Launcher");
    Directory.CreateDirectory(oldLauncherDir);
    await File.WriteAllTextAsync(Path.Combine(oldLauncherDir, "OSFRLauncher"), "old-launcher");
    await File.WriteAllTextAsync(Path.Combine(transactionRoot, InstallationOwnership.LegacyInstallInfoFileName), "old-info");
    await File.WriteAllTextAsync(Path.Combine(transactionRoot, InstallationOwnership.MarkerFileName), "Sanctuary Linux Installer");

    var staged = Path.Combine(transactionRoot, ".staged-test");
    Directory.CreateDirectory(staged);
    await File.WriteAllTextAsync(Path.Combine(staged, "OSFRLauncher"), "new-launcher");

    var transaction = new InstallationTransaction(transactionRoot);
    transaction.Begin();
    transaction.Promote(staged);
    await File.WriteAllTextAsync(Path.Combine(transactionRoot, InstallationOwnership.LegacyInstallInfoFileName), "new-info");
    transaction.Rollback();

    Assert(await File.ReadAllTextAsync(Path.Combine(transactionRoot, "Launcher", "OSFRLauncher")) == "old-launcher", "Rollback must restore the previous launcher.");
    Assert(await File.ReadAllTextAsync(Path.Combine(transactionRoot, InstallationOwnership.LegacyInstallInfoFileName)) == "old-info", "Rollback must restore the previous install metadata.");
    Assert(await File.ReadAllTextAsync(Path.Combine(transactionRoot, InstallationOwnership.MarkerFileName)) == "Sanctuary Linux Installer", "Rollback must restore the previous ownership marker.");
}
finally
{
    if (Directory.Exists(transactionRoot))
        Directory.Delete(transactionRoot, true);
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
        Assert(InstallService.HasSymbolicLinkAncestor(Path.Combine(link, "Sanctuary")), "A symbolic-link ancestor must be detected even when the final install directory does not exist yet.");
        Assert(InstallService.GetInstallDestinationError(Path.Combine(link, "Sanctuary")) is not null, "Install paths below symbolic-link ancestors must be rejected.");
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

    createDesktopEntries.Invoke(null, ["/tmp/OSFR Test/OSFRLauncher", true]);
    var generatedDesktop = Path.Combine(home, ".local", "share", "applications", "OSFR-Linux.desktop");
    Assert(File.Exists(generatedDesktop), "CI desktop-entry generation must produce an application-menu entry.");

    var desktopText = await File.ReadAllTextAsync(generatedDesktop);
    Assert(desktopText.Contains("Name=Sanctuary", StringComparison.Ordinal), "Desktop and application-menu entries must display Sanctuary as the application name.");
    Assert(!desktopText.Contains("Name=Open Source Free Realms", StringComparison.Ordinal), "Legacy Open Source Free Realms shortcut naming must not remain user-facing.");
    Assert(desktopText.Contains("Icon=osfr-linux", StringComparison.Ordinal), "Desktop entry must use the stable icon-theme name.");
    Assert(desktopText.Contains("StartupWMClass=OSFRLauncher", StringComparison.Ordinal), "Desktop entry must declare the launcher window class for taskbar grouping.");
    createDesktopEntries.Invoke(null, ["/tmp/OSFR Test/OSFRLauncher", false]);
    Assert(!File.Exists(InstallService.DesktopShortcutPath), "Disabling the desktop shortcut must remove only the optional Desktop copy.");
    Assert(File.Exists(generatedDesktop), "Disabling the desktop shortcut must preserve application-menu integration.");
    Console.WriteLine($"Generated desktop entry: {generatedDesktop}");
}

Console.WriteLine("Installer safety smoke tests passed.");
