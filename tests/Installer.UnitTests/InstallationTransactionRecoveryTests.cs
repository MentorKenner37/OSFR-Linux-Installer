using System;
using System.IO;
using System.Runtime.InteropServices;
using OSFR.Linux.Installer.Services;
using Xunit;

namespace Installer.UnitTests
{
    public class InstallationTransactionRecoveryTests : IDisposable
    {
        private readonly TempDirFixture _fixture = new();
        public void Dispose() => _fixture.Dispose();

        [Fact]
        public void RecoverIfNeeded_deletes_new_launcher_when_hadLauncher_false()
        {
            var installRoot = _fixture.Root;
            var launcher = Path.Combine(installRoot, "Launcher");
            Directory.CreateDirectory(launcher);
            File.WriteAllText(Path.Combine(launcher, "OSFRLauncher"), "new");

            var stateFile = Path.Combine(installRoot, ".sanctuary-install-transaction.json");
            var doc = System.Text.Json.JsonSerializer.Serialize(new
            {
                Version = 2,
                State = "active",
                HadLauncher = false,
                HadOwnershipMarker = false,
                HadLegacyInfo = false,
                UpdatedUtc = DateTimeOffset.UtcNow
            });
            File.WriteAllText(stateFile, doc);

            InstallationTransaction.RecoverIfNeeded(installRoot);

            Assert.False(Directory.Exists(launcher));
            Assert.False(File.Exists(stateFile));
        }

        [Fact]
        public void RecoverIfNeeded_committed_cleans_backups_and_statefile()
        {
            var installRoot = _fixture.Root;
            var backup = Path.Combine(installRoot, ".launcher-backup");
            Directory.CreateDirectory(backup);
            File.WriteAllText(Path.Combine(backup, "OSFRLauncher"), "backup");

            var markerBackup = Path.Combine(installRoot, ".ownership-backup");
            File.WriteAllText(markerBackup, "marker");

            var legacyBackup = Path.Combine(installRoot, ".install-info-backup");
            File.WriteAllText(legacyBackup, "info");

            var stateFile = Path.Combine(installRoot, ".sanctuary-install-transaction.json");
            var doc = System.Text.Json.JsonSerializer.Serialize(new
            {
                Version = 2,
                State = "committed",
                HadLauncher = true,
                HadOwnershipMarker = true,
                HadLegacyInfo = true,
                UpdatedUtc = DateTimeOffset.UtcNow
            });
            File.WriteAllText(stateFile, doc);

            InstallationTransaction.RecoverIfNeeded(installRoot);

            Assert.False(Directory.Exists(backup));
            Assert.False(File.Exists(markerBackup));
            Assert.False(File.Exists(legacyBackup));
            Assert.False(File.Exists(stateFile));
        }
    }
}
