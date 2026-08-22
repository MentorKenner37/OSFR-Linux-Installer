using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using HashDepot;

namespace Launcher.Helpers;

public static class CurlDownloadHelper
{
    public static string? FindExecutable(string? pathValue = null)
    {
        var path = pathValue ?? Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = Path.Combine(directory, "curl");
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        foreach (var candidate in new[] { "/usr/bin/curl", "/bin/curl" })
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    public static ProcessStartInfo CreateStartInfo(string curlPath, string sourceUrl, string outputPath, uint maximumBytes)
    {
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException("curl downloads must use an absolute HTTPS URL.");

        var startInfo = new ProcessStartInfo(curlPath)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--fail");
        startInfo.ArgumentList.Add("--silent");
        startInfo.ArgumentList.Add("--show-error");
        startInfo.ArgumentList.Add("--location");
        startInfo.ArgumentList.Add("--proto");
        startInfo.ArgumentList.Add("=https");
        startInfo.ArgumentList.Add("--proto-redir");
        startInfo.ArgumentList.Add("=https");
        startInfo.ArgumentList.Add("--tlsv1.2");
        startInfo.ArgumentList.Add("--max-filesize");
        startInfo.ArgumentList.Add(maximumBytes.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(outputPath);
        startInfo.ArgumentList.Add(uri.AbsoluteUri);
        return startInfo;
    }

    public static void VerifyFile(string path, uint expectedSize, ulong expectedHash)
    {
        using var stream = File.OpenRead(path);
        var actualSize = stream.Length;
        var actualHash = XXHash.Hash64(stream);
        if (actualSize != expectedSize || actualHash != expectedHash)
            throw new InvalidDataException(
                $"curl download failed verification: expected-size={expectedSize}, received-size={actualSize}, " +
                $"expected-xxhash64={expectedHash}, received-xxhash64={actualHash}.");
    }

    public static async Task DownloadAndVerifyAsync(
        string curlPath,
        string sourceUrl,
        string outputPath,
        uint expectedSize,
        ulong expectedHash,
        CancellationToken cancellationToken)
    {
        using var process = Process.Start(CreateStartInfo(curlPath, sourceUrl, outputPath, expectedSize))
            ?? throw new InvalidOperationException("Failed to start curl.");
        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // The process exited between HasExited and Kill.
            }
        });

        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var error = await errorTask;
        if (process.ExitCode != 0)
            throw new InvalidDataException($"curl exited with code {process.ExitCode}: {error.Trim()}");

        VerifyFile(outputPath, expectedSize, expectedHash);
    }
}
