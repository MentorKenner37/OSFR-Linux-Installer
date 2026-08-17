using Launcher.Extensions;
using Launcher.Helpers;

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static bool Throws<T>(Action action) where T : Exception
{
    try
    {
        action();
        return false;
    }
    catch (T)
    {
        return true;
    }
}

var safeServer = ServerPathHelper.GetServerDirectory("SmokeServer");
Assert(safeServer.StartsWith(ServerPathHelper.ServersRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal),
    "Safe server paths must remain below the Servers root.");

Assert(Throws<InvalidDataException>(() => ServerPathHelper.GetServerDirectory("../escape")),
    "Parent traversal must not escape the Servers root.");
Assert(Throws<InvalidDataException>(() => ServerPathHelper.GetServerDirectory("/tmp/outside-osfr")),
    "Absolute paths outside the Servers root must be rejected.");
Assert(Throws<InvalidDataException>(() => ServerPathHelper.GetClientDirectory("SmokeServer", "../../escape")),
    "Client folder traversal must be rejected.");
Assert(Throws<InvalidDataException>(() => ServerPathHelper.GetClientFilePath("SmokeServer", "", "../../escape.bin")),
    "Client file traversal must be rejected.");

var sanitized = "../../Bad/Server\\Name".ToValidDirectoryName();
Assert(!sanitized.Contains('/') && !sanitized.Contains('\\') && sanitized is not "." and not "..",
    "Server names must be converted to safe single directory names.");

var protonConfig = Path.Combine(AppContext.BaseDirectory, "proton-path.txt");
var graphicsConfig = Path.Combine(AppContext.BaseDirectory, "graphics-backend.txt");
var fakeProton = Path.Combine(AppContext.BaseDirectory, "smoke-proton");
try
{
    File.WriteAllText(fakeProton, "stub");
    File.WriteAllText(protonConfig, fakeProton + Environment.NewLine);

    File.WriteAllText(graphicsConfig, "wined3d\n");
    _ = ProtonHelper.GetPath();
    Assert(Environment.GetEnvironmentVariable("PROTON_USE_WINED3D") == "1",
        "WineD3D selection must enable PROTON_USE_WINED3D for Proton child processes.");

    File.WriteAllText(graphicsConfig, "dxvk\n");
    _ = ProtonHelper.GetPath();
    Assert(Environment.GetEnvironmentVariable("PROTON_USE_WINED3D") == "0",
        "DXVK selection must explicitly disable PROTON_USE_WINED3D.");

    File.Delete(graphicsConfig);
    Environment.SetEnvironmentVariable("PROTON_USE_WINED3D", "1");
    _ = ProtonHelper.GetPath();
    Assert(Environment.GetEnvironmentVariable("PROTON_USE_WINED3D") == "0",
        "Missing graphics configuration must default Sanctuary to DXVK rather than inheriting a parent-shell WineD3D override.");
}
finally
{
    File.Delete(graphicsConfig);
    File.Delete(protonConfig);
    File.Delete(fakeProton);
}

Console.WriteLine("Launcher path and graphics backend safety smoke tests passed.");
