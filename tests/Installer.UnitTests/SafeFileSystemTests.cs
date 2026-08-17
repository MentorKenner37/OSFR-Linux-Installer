using Xunit;
using OSFR.Linux.Installer.Services;

namespace Installer.UnitTests
{
    public class SafeFileSystemTests
    {
        [Theory]
        [InlineData("file.txt", true)]
        [InlineData("../evil.txt", false)]
        [InlineData("/absolute", false)]
        [InlineData("dir/../file", false)]
        [InlineData("dir/sub/file", true)]
        public void IsSafeArchiveEntry_checks_paths(string entry, bool expected)
        {
            Assert.Equal(expected, SafeFileSystem.IsSafeArchiveEntry(entry));
        }
    }
}
