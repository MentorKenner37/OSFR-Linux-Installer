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
        }
    }
}
