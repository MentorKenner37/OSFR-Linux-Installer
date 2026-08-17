using System.Reflection;

namespace OSFR.Linux.Installer;

internal static class OsfrBranding
{
    private const string ResourceName = "OSFR.Linux.Installer.Icon";
    private static readonly Lazy<byte[]> IconBytes = new(LoadIconBytes);

    public static ReadOnlyMemory<byte> Bytes => IconBytes.Value;

    public static MemoryStream OpenIconStream() => new(IconBytes.Value, writable: false);

    public static async Task WriteIconAsync(string path, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllBytesAsync(path, IconBytes.Value, cancellationToken);
    }

    private static byte[] LoadIconBytes()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The embedded OSFR branding icon is missing.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var bytes = memory.ToArray();

        if (bytes.Length < 8 || bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47)
            throw new InvalidDataException("The embedded OSFR branding icon is not a valid PNG payload.");

        return bytes;
    }
}
