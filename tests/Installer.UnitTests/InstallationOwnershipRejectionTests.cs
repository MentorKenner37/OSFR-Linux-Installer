using System;
using System.IO;
using System.Text.Json;
using OSFR.Linux.Installer.Services;
using Xunit;

namespace Installer.UnitTests
{
    public class InstallationOwnershipRejectionTests : IDisposable
    {
        private readonly TempDirFixture _fixture = new();
        public void Dispose() => _fixture.Dispose();

        private static string WriteOwnershipDocument(string installRoot, object doc)
        {
            var marker = Path.Combine(installRoot, InstallationOwnership.MarkerFileName);
            Directory.CreateDirectory(installRoot);
            File.WriteAllText(marker, JsonSerializer.Serialize(doc));
            return marker;
        }

        [Fact]
        public void IsOwned_rejects_corrupted_metadata()
        {
            var installRoot = _fixture.Root;
            var launcherDir = Path.Combine(installRoot, "Launcher");
            Directory.CreateDirectory(launcherDir);
            File.WriteAllText(Path.Combine(launcherDir, "OSFRLauncher"), "binary");

            var marker = Path.Combine(installRoot, InstallationOwnership.MarkerFileName);
            File.WriteAllText(marker, "{ not valid json }");

            Assert.False(InstallationOwnership.IsOwned(installRoot));
        }

        [Fact]
        public void IsOwned_rejects_wrong_install_root()
        {
            var installRoot = _fixture.Root;
            var launcherDir = Path.Combine(installRoot, "Launcher");
            Directory.CreateDirectory(launcherDir);
            var launcherFile = Path.Combine(launcherDir, "OSFRLauncher");
            File.WriteAllText(launcherFile, "binary");

            var doc = new
            {
                Product = "SanctuaryLinuxInstaller",
                FormatVersion = 2,
                InstallId = Guid.NewGuid().ToString("D"),
                InstallRoot = "/some/other/path",
                Launcher = "Launcher/OSFRLauncher",
                InstallerVersion = "0.0",
                LauncherSha256 = InstallationOwnership.ComputeSha256(launcherFile),
                CreatedUtc = DateTimeOffset.UtcNow
            };

            // write marker with wrong InstallRoot
            var marker = Path.Combine(installRoot, InstallationOwnership.MarkerFileName);
            File.WriteAllText(marker, JsonSerializer.Serialize(doc));

            Assert.False(InstallationOwnership.IsOwned(installRoot));
        }

        [Fact]
        public void IsOwned_rejects_mismatched_launcher_sha()
        {
            var installRoot = _fixture.Root;
            var launcherDir = Path.Combine(installRoot, "Launcher");
            Directory.CreateDirectory(launcherDir);
            var launcherFile = Path.Combine(launcherDir, "OSFRLauncher");
            File.WriteAllText(launcherFile, "binary");

            var doc = new
            {
                Product = "SanctuaryLinuxInstaller",
                FormatVersion = 2,
                InstallId = Guid.NewGuid().ToString("D"),
                InstallRoot = installRoot,
                Launcher = "Launcher/OSFRLauncher",
                InstallerVersion = "0.0",
                LauncherSha256 = "deadbeef",
                CreatedUtc = DateTimeOffset.UtcNow
            };

            var marker = Path.Combine(installRoot, InstallationOwnership.MarkerFileName);
            File.WriteAllText(marker, JsonSerializer.Serialize(doc));

            Assert.False(InstallationOwnership.IsOwned(installRoot));
        }

        [Fact]
        public void IsOwned_rejects_missing_launcher()
        {
            var installRoot = _fixture.Root;
            Directory.CreateDirectory(installRoot);
            var doc = new
            {
                Product = "SanctuaryLinuxInstaller",
                FormatVersion = 2,
                InstallId = Guid.NewGuid().ToString("D"),
                InstallRoot = installRoot,
                Launcher = "Launcher/OSFRLauncher",
                InstallerVersion = "0.0",
                LauncherSha256 = "deadbeef",
                CreatedUtc = DateTimeOffset.UtcNow
            };

            var marker = Path.Combine(installRoot, InstallationOwnership.MarkerFileName);
            File.WriteAllText(marker, JsonSerializer.Serialize(doc));

            Assert.False(InstallationOwnership.IsOwned(installRoot));
        }

        [Fact]
        public void IsOwned_rejects_symlinked_marker_or_launcher()
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
                return; // symlink behavior tested on Unix-like platforms in CI

            var installRoot = _fixture.Root;
            var launcherDir = Path.Combine(installRoot, "Launcher");
            Directory.CreateDirectory(launcherDir);
            var launcherFile = Path.Combine(launcherDir, "OSFRLauncher");
            File.WriteAllText(launcherFile, "binary");

            var marker = Path.Combine(installRoot, InstallationOwnership.MarkerFileName);
            File.WriteAllText(marker, "Sanctuary Linux Installer");

            // Replace marker with symlink
            var realMarkerDir = _fixture.CreateDir("marker-target");
            var realMarker = Path.Combine(realMarkerDir, "marker");
            File.WriteAllText(realMarker, "Sanctuary Linux Installer");

            File.Delete(marker);
            File.CreateSymbolicLink(marker, realMarker);

            Assert.False(InstallationOwnership.IsOwned(installRoot));

            // Now replace launcher with symlink
            var realLauncherDir = _fixture.CreateDir("real-launcher");
            var realLauncher = Path.Combine(realLauncherDir, "OSFRLauncher");
            File.WriteAllText(realLauncher, "binary");

            File.Delete(launcherFile);
            File.CreateSymbolicLink(launcherFile, realLauncher);

            Assert.False(InstallationOwnership.IsOwned(installRoot));
        }
    }
}
