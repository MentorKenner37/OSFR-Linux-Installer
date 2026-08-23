using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace OSFR.Linux.Installer.Services;

public static partial class DiagnosticBundleService
{
    private const int MaximumLogBytes = 2_000_000;

    public static async Task CreateAsync(string destination, string systemSummary, CancellationToken cancellationToken = default)
    {
        destination = Path.GetFullPath(destination);
        var parent = Path.GetDirectoryName(destination)
            ?? throw new InvalidOperationException("The diagnostic bundle destination is invalid.");
        Directory.CreateDirectory(parent);
        SafeFileSystem.RefuseSymbolicLink(parent, "diagnostic bundle directory");
        SafeFileSystem.RefuseSymbolicLink(destination, "diagnostic bundle file");

        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: false);
        await AddTextAsync(archive, "system.txt", Redact(systemSummary), cancellationToken);

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        await AddLogIfPresentAsync(archive, InstallerLog.LogPath, "installer.log", cancellationToken);
        await AddLogIfPresentAsync(
            archive,
            Path.Combine(home, ".local", "share", "OSFR-Linux", "Launcher", "logs", "Launcher.log"),
            "launcher.log",
            cancellationToken);
    }

    public static string Redact(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        value = SecretAssignmentRegex().Replace(value, "$1=<redacted>");
        return SensitiveQueryRegex().Replace(value, "$1=<redacted>");
    }

    private static async Task AddLogIfPresentAsync(ZipArchive archive, string path, string entryName, CancellationToken cancellationToken)
    {
        if (!File.Exists(path) || InstallService.IsSymbolicLink(path))
            return;

        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var length = (int)Math.Min(input.Length, MaximumLogBytes);
        if (input.Length > length)
            input.Position = input.Length - length;
        var bytes = new byte[length];
        var read = await input.ReadAtLeastAsync(bytes, bytes.Length, throwOnEndOfStream: false, cancellationToken);
        await AddTextAsync(archive, entryName, Redact(Encoding.UTF8.GetString(bytes, 0, read)), cancellationToken);
    }

    private static async Task AddTextAsync(ZipArchive archive, string name, string text, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: false);
        await writer.WriteAsync(text.AsMemory(), cancellationToken);
    }

    [GeneratedRegex(@"(?i)\b(sessionid|password|passwd|authorization|bearer|access[_-]?token|refresh[_-]?token|cookie)\s*[:=]\s*([^\s&;]+)")]
    private static partial Regex SecretAssignmentRegex();

    [GeneratedRegex(@"(?i)([?&](?:sessionid|token|access_token|refresh_token|password))=[^&#\s]+")]
    private static partial Regex SensitiveQueryRegex();
}
