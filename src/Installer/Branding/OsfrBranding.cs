using System.Reflection;

namespace OSFR.Linux.Installer;

internal static class OsfrBranding
{
    private const string ResourcePrefix = "OSFR.Linux.Installer.Branding.osfr_icon_b64_";
    private static readonly Lazy<byte[]> IconBytes = new(LoadIconBytes);

    public static ReadOnlyMemory<byte> Bytes => IconBytes.Value;

    public static MemoryStream OpenIconStream() => new(IconBytes.Value, writable: false);

    public static async Task WriteIconAsync(string path, CancellationToken cancellationToken = default)
    {
        await File.WriteAllBytesAsync(path, IconBytes.Value, cancellationToken);
    }

    private static byte[] LoadIconBytes()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var names = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        if (names.Length == 0)
            throw new InvalidOperationException("The embedded OSFR branding icon is missing.");

        var encoded = new System.Text.StringBuilder();
        foreach (var name in names)
        {
            using var stream = assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Could not load icon resource {name}.");
            using var reader = new StreamReader(stream);
            encoded.Append(reader.ReadToEnd().Trim());
        }

        var bytes = Convert.FromBase64String(encoded.ToString());
        if (bytes.Length < 8 || bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47)
            throw new InvalidDataException("The embedded OSFR branding icon is not a valid PNG payload.");

        return bytes;
    }
}
