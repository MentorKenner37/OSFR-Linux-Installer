using System;
using System.IO;
using OSFR.Linux.Installer.Services;
using Xunit;

namespace Installer.UnitTests
{
    public class InstallServiceValidationTests : IDisposable
    {
        private readonly TempDirFixture _fixture = new();
        public void Dispose() => _fixture.Dispose();

        [Fact]
        public void GetInstallDestinationError_rejects_symbolic_link_ancestor()
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
                return;

            var real = _fixture.CreateDir("realparent");
            var symlink = Path.Combine(_fixture.Root, "linkparent");
            Directory.CreateSymbolicLink(symlink, real);

            var candidate = Path.Combine(symlink, "target");
            var error = InstallService.GetInstallDestinationError(candidate);
            Assert.Equal("The installation path contains a symbolic-link directory. Choose a direct filesystem path instead.", error);
        }

        [Fact]
        public void GetInstallDestinationError_accepts_existing_transaction_statefile()
        {
            var installRoot = _fixture.Root;
            Directory.CreateDirectory(installRoot);
            File.WriteAllText(Path.Combine(installRoot, ".sanctuary-install-transaction.json"), "{}");

            var error = InstallService.GetInstallDestinationError(installRoot);
            Assert.Null(error);
        }

        [Fact]
        public void GetInstallDestinationError_rejects_file_path()
        {
            var file = _fixture.CreateFile("somefile.txt", "data");
            var error = InstallService.GetInstallDestinationError(file);
            Assert.Equal("The selected path is a file. Choose a folder instead.", error);
        }
    }
}
