using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Launcher.ViewModels;
using NLog;

namespace Launcher.Services;

internal static class UpdateService
{
    private const string ReleasesApi = "https://api.github.com/repos/MentorKenner37/OSFR-Linux-Installer/releases?per_page=20";
    private const string InstallerAsset = "Sanctuary-Linux-Installer";
    private const string ChecksumAsset = "Sanctuary-Linux-Installer.sha256";
    private static readonly HttpClient Http = CreateClient();
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static ReleaseInfo? _available;

    internal sealed record ReleaseInfo(string Tag, string Version, bool Prerelease, string Notes, string InstallerUrl, string ChecksumUrl);
    private sealed record GitHubAsset([property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("browser_download_url")] string Url);
    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string Tag,
        [property: JsonPropertyName("prerelease")] bool Prerelease,
        [property: JsonPropertyName("draft")] bool Draft,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("assets")] List<GitHubAsset> Assets);

    public static async Task CheckAsync(bool interactive)
    {
        try
        {
            if (!interactive && !ShouldCheckNow())
                return;

            Settings.Instance.LastUpdateCheckUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            Settings.Instance.Save();
            var json = await Http.GetStringAsync(ReleasesApi);
            var releases = JsonSerializer.Deserialize<List<GitHubRelease>>(json) ?? [];
            var allowPrerelease = Settings.Instance.BetaUpdates;
            var release = releases.FirstOrDefault(item => !item.Draft && (allowPrerelease || !item.Prerelease));
            if (release is null || !TryCreateReleaseInfo(release, out var info) || CompareVersions(info.Version, App.CurrentVersion) <= 0)
            {
                Settings.Instance.AvailableUpdateStatus = $"Up to date — {App.CurrentVersion}";
                if (interactive)
                    App.AddNotification($"Sanctuary is up to date ({App.CurrentVersion}).");
                return;
            }

            _available = info;
            Settings.Instance.AvailableUpdateStatus = $"Available: {info.Tag}";
            if (Settings.Instance.SkippedUpdateVersion == info.Tag && !interactive)
                return;

            App.AddNotification($"Sanctuary update {info.Tag} is available. Open Settings → Updates to install it or review the release notes.\n\n{Summarize(info.Notes)}");
            if (Settings.Instance.AutomaticUpdates)
                await DownloadVerifyAndLaunchAsync();
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Sanctuary update check failed; offline launch remains available.");
            Settings.Instance.AvailableUpdateStatus = "Update check unavailable — offline launch is unaffected";
            if (interactive)
                App.AddNotification($"Update check failed: {ex.Message}", true);
        }
    }

    public static async Task DownloadVerifyAndLaunchAsync()
    {
        if (_available is null)
            await CheckAsync(true);
        var release = _available ?? throw new InvalidOperationException("No newer Sanctuary release is available.");

        var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "Sanctuary", "updates", release.Tag);
        Directory.CreateDirectory(cache);
        var installer = Path.Combine(cache, InstallerAsset);
        var checksum = Path.Combine(cache, ChecksumAsset);
        await File.WriteAllBytesAsync(installer, await Http.GetByteArrayAsync(release.InstallerUrl));
        await File.WriteAllBytesAsync(checksum, await Http.GetByteArrayAsync(release.ChecksumUrl));
        VerifyChecksum(installer, await File.ReadAllTextAsync(checksum));
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("Sanctuary automatic updates currently require Linux.");
        File.SetUnixFileMode(installer, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var launcherDirectory = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var installRoot = Directory.GetParent(launcherDirectory)?.FullName
            ?? throw new InvalidOperationException("Could not determine the Sanctuary installation directory.");
        if (!File.Exists(Path.Combine(installRoot, ".osfr-linux-install")))
            throw new InvalidOperationException("Automatic upgrade requires a verified Sanctuary installation.");

        var start = new ProcessStartInfo(installer) { UseShellExecute = false, WorkingDirectory = cache };
        start.ArgumentList.Add("--auto-upgrade");
        start.ArgumentList.Add("--install-root");
        start.ArgumentList.Add(installRoot);
        start.ArgumentList.Add("--wait-pid");
        start.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        _ = Process.Start(start) ?? throw new InvalidOperationException("Could not start the verified Sanctuary updater.");
        Logger.Info("Verified update {Tag} launched; closing the old launcher before transactional replacement.", release.Tag);
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            lifetime.Shutdown();
    }

    public static void SkipAvailable()
    {
        if (_available is null)
            return;
        Settings.Instance.SkippedUpdateVersion = _available.Tag;
        Settings.Instance.AvailableUpdateStatus = $"Skipped: {_available.Tag}";
        Settings.Instance.Save();
    }

    private static bool ShouldCheckNow() =>
        !DateTimeOffset.TryParse(Settings.Instance.LastUpdateCheckUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var last)
        || DateTimeOffset.UtcNow - last >= TimeSpan.FromHours(12);

    private static bool TryCreateReleaseInfo(GitHubRelease release, out ReleaseInfo info)
    {
        var installer = release.Assets.FirstOrDefault(asset => asset.Name == InstallerAsset)?.Url;
        var checksum = release.Assets.FirstOrDefault(asset => asset.Name == ChecksumAsset)?.Url;
        if (installer is null || checksum is null)
        {
            info = null!;
            return false;
        }
        info = new(release.Tag, release.Tag.TrimStart('v'), release.Prerelease, release.Body ?? string.Empty, installer, checksum);
        return true;
    }

    private static int CompareVersions(string left, string right)
    {
        static (Version Core, int Pre) Parse(string value)
        {
            value = value.TrimStart('v').Split('+', 2)[0];
            var parts = value.Split('-', 2);
            var core = Version.TryParse(parts[0], out var parsed) ? parsed : new Version();
            if (parts.Length == 1)
                return (core, int.MaxValue);
            var digits = new string(parts[1].Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
            return (core, int.TryParse(digits, out var pre) ? pre : 0);
        }
        var a = Parse(left);
        var b = Parse(right);
        var coreResult = a.Core.CompareTo(b.Core);
        return coreResult != 0 ? coreResult : a.Pre.CompareTo(b.Pre);
    }

    private static void VerifyChecksum(string installer, string checksumText)
    {
        var expected = checksumText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (expected is null || expected.Length != 64 || expected.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidDataException("The published update checksum is malformed.");
        using var stream = File.OpenRead(installer);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The downloaded Sanctuary installer failed SHA-256 verification.");
    }

    private static string Summarize(string notes)
    {
        var text = string.Join(' ', notes.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#')));
        return text.Length <= 350 ? text : text[..350] + "…";
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Sanctuary-Linux-Launcher/1.0");
        return client;
    }
}
