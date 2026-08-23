using System.IO;
using Xunit;
using OSFR.Linux.Installer.Services;

namespace Installer.UnitTests
{
    public class InstallationOwnershipTests : System.IDisposable
    {
        private readonly TempDirFixture _fixture = new();

        public void Dispose() => _fixture.Dispose();

        [Fact]
        public void Write_and_IsOwned_roundtrip()
        {
            var installRoot = _fixture.Root;
            var launcherDir = Path.Combine(installRoot, "Launcher");
            Directory.CreateDirectory(launcherDir);
            var launcherFile = Path.Combine(launcherDir, "OSFRLauncher");
            File.WriteAllText(launcherFile, "binary");

            InstallationOwnership.Write(installRoot);

            Assert.True(InstallationOwnership.IsOwned(installRoot));
            Assert.True(InstallationOwnership.HasRecognizableMetadata(installRoot));
            Assert.False(string.IsNullOrWhiteSpace(InstallationOwnership.GetInstalledVersion(installRoot)));
        }

        [Fact]
        public void Damaged_launcher_is_recognized_for_repair_but_not_uninstall()
        {
            var installRoot = _fixture.Root;
            var launcherDir = Path.Combine(installRoot, "Launcher");
            Directory.CreateDirectory(launcherDir);
            var launcherFile = Path.Combine(launcherDir, "OSFRLauncher");
            File.WriteAllText(launcherFile, "original");
            InstallationOwnership.Write(installRoot);

            File.WriteAllText(launcherFile, "damaged");

            Assert.False(InstallationOwnership.IsOwned(installRoot));
            Assert.True(InstallationOwnership.HasRecognizableMetadata(installRoot));
            Assert.Equal(InstallationCondition.NeedsRepair, new InstallService().GetInstallationInfo(installRoot).Condition);
        }
    }
}
