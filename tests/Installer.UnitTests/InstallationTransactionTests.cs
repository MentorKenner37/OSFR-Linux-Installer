using System;
using System.IO;
using OSFR.Linux.Installer.Services;
using Xunit;

namespace Installer.UnitTests
{
    public class InstallationTransactionTests : IDisposable
    {
        private readonly TempDirFixture _fixture = new();

        public void Dispose() => _fixture.Dispose();

        [Fact]
        public void Begin_and_Rollback_restores_existing_launcher()
        {
            var installRoot = _fixture.Root;
            var launcher = Path.Combine(installRoot, "Launcher");
            Directory.CreateDirectory(launcher);
            var originalFile = Path.Combine(launcher, "OSFRLauncher");
            File.WriteAllText(originalFile, "original");

            var tx = new InstallationTransaction(installRoot);
            tx.Begin();

            // After begin, launcher moved to backup
            Assert.False(Directory.Exists(launcher));
            var backup = Path.Combine(installRoot, ".launcher-backup");
            Assert.True(Directory.Exists(backup));

            // Create staged launcher and promote
            var staged = Path.Combine(_fixture.Root, "staged");
            Directory.CreateDirectory(staged);
            var promotedFile = Path.Combine(staged, "OSFRLauncher");
            File.WriteAllText(promotedFile, "promoted");

            tx.Promote(staged);

            // After promote, launcher dir exists with promoted content
            Assert.True(Directory.Exists(launcher));
            Assert.Equal("promoted", File.ReadAllText(Path.Combine(launcher, "OSFRLauncher")));

            // Rollback should restore original launcher from backup
            tx.Rollback();

            Assert.True(Directory.Exists(launcher));
            Assert.Equal("original", File.ReadAllText(Path.Combine(launcher, "OSFRLauncher")));
            // statefile should be absent after rollback
            Assert.False(File.Exists(Path.Combine(installRoot, ".sanctuary-install-transaction.json")));
        }

        [Fact]
        public void Commit_cleans_backup_and_removes_statefile()
        {
            var installRoot = _fixture.Root;
            var launcher = Path.Combine(installRoot, "Launcher");
            Directory.CreateDirectory(launcher);
            var originalFile = Path.Combine(launcher, "OSFRLauncher");
            File.WriteAllText(originalFile, "original");

            var tx = new InstallationTransaction(installRoot);
            tx.Begin();

            var staged = Path.Combine(_fixture.Root, "staged");
            Directory.CreateDirectory(staged);
            var promotedFile = Path.Combine(staged, "OSFRLauncher");
            File.WriteAllText(promotedFile, "promoted");

            tx.Promote(staged);
            tx.Commit();

            var backup = Path.Combine(installRoot, ".launcher-backup");
            Assert.False(Directory.Exists(backup));
            Assert.False(File.Exists(Path.Combine(installRoot, ".sanctuary-install-transaction.json")));
            Assert.True(Directory.Exists(launcher));
            Assert.Equal("promoted", File.ReadAllText(Path.Combine(launcher, "OSFRLauncher")));
        }

        [Fact]
        public void RecoverIfNeeded_restores_from_backup_when_active()
        {
            var installRoot = _fixture.Root;
            var backup = Path.Combine(installRoot, ".launcher-backup");
            Directory.CreateDirectory(backup);
            var backupFile = Path.Combine(backup, "OSFRLauncher");
            File.WriteAllText(backupFile, "from-backup");

            // Create a state file indicating active with HadLauncher true
            var stateFile = Path.Combine(installRoot, ".sanctuary-install-transaction.json");
            var doc = System.Text.Json.JsonSerializer.Serialize(new
            {
                Version = 2,
                State = "active",
                HadLauncher = true,
                HadOwnershipMarker = false,
                HadLegacyInfo = false,
                UpdatedUtc = DateTimeOffset.UtcNow
            });
            File.WriteAllText(stateFile, doc);

            // Ensure no Launcher dir exists
            InstallationTransaction.RecoverIfNeeded(installRoot);

            var launcher = Path.Combine(installRoot, "Launcher");
            Assert.True(Directory.Exists(launcher));
            Assert.Equal("from-backup", File.ReadAllText(Path.Combine(launcher, "OSFRLauncher")));
            Assert.False(File.Exists(stateFile));
        }
    }
}
