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

Console.WriteLine("Launcher path safety smoke tests passed.");
