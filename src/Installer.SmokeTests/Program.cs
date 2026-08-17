using OSFR.Linux.Installer.Services;

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
var normalized = InstallService.NormalizeInstallRoot("~/OSFR-Smoke-Test");
Assert(normalized == Path.GetFullPath(Path.Combine(home, "OSFR-Smoke-Test")), "Tilde install paths must resolve under the user home directory.");

var ready = new SystemState(true, true, "/tmp/fake-steam", "/tmp/fake-proton");
Assert(ready.Ready, "A complete Linux x64 Steam/Proton state should be ready.");

var notReady = new SystemState(true, true, null, "/tmp/fake-proton");
Assert(!notReady.Ready, "Steam is required for readiness.");

var service = new InstallService();
var tempRoot = Path.Combine(Path.GetTempPath(), $"osfr-installer-smoke-{Guid.NewGuid():N}");
Directory.CreateDirectory(tempRoot);
var sentinel = Path.Combine(tempRoot, "do-not-delete.txt");
await File.WriteAllTextAsync(sentinel, "unrelated user data");

try
{
    var installRejected = false;
    try
    {
        await service.InstallAsync(tempRoot, ready, new Progress<InstallProgress>());
    }
    catch (InvalidOperationException)
    {
        installRejected = true;
    }

    Assert(installRejected, "Install must reject a non-empty unrelated directory.");
    Assert(File.Exists(sentinel), "Rejected install destinations must remain untouched.");

    var uninstallRejected = false;
    try
    {
        await service.UninstallAsync(tempRoot, new Progress<InstallProgress>());
    }
    catch (InvalidOperationException)
    {
        uninstallRejected = true;
    }

    Assert(uninstallRejected, "Uninstall must reject a directory it does not own.");
    Assert(File.Exists(sentinel), "Uninstall must never delete unowned user data.");
}
finally
{
    if (Directory.Exists(tempRoot))
        Directory.Delete(tempRoot, true);
}

Console.WriteLine("Installer safety smoke tests passed.");
