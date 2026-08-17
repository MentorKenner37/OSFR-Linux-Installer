using System;
using System.IO;
using System.Runtime.InteropServices;
using OSFR.Linux.Installer.Services;
using Xunit;

namespace Installer.UnitTests
{
    public class SafeFileSystemSymlinkTests : IDisposable
    {
        private readonly TempDirFixture _fixture = new();
        public void Dispose() => _fixture.Dispose();

        [Fact]
        public void RefuseSymbolicLink_throws_on_direct_symlink()
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
                return;

            var target = _fixture.CreateDir("target");
            var link = Path.Combine(_fixture.Root, "linkdir");
            Directory.CreateSymbolicLink(link, target);

            var ex = Assert.Throws<InvalidOperationException>(() => SafeFileSystem.RefuseSymbolicLink(link, "test link"));
            Assert.Contains("symbolic link", ex.Message);
        }

        [Fact]
        public void HasSymbolicLinkAncestor_detects_symlinked_parent()
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
                return;

            var realParent = _fixture.CreateDir("realparent");
            var linkParent = Path.Combine(_fixture.Root, "linkparent");
            Directory.CreateSymbolicLink(linkParent, realParent);

            var child = Path.Combine(linkParent, "child");
            Directory.CreateDirectory(child);

            Assert.True(SafeFileSystem.HasSymbolicLinkAncestor(child));
        }
    }
}
