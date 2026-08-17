using System;
using OSFR.Linux.Installer.Services;
using Xunit;

namespace Installer.UnitTests
{
    public class ArchiveTraversalExtendedTests
    {
        [Theory]
        [InlineData("../evil.txt", false)]
        [InlineData("..\\evil.txt", false)]
        [InlineData("/absolute/path", false)]
        [InlineData("C:\\Windows\\system32", false)]
        [InlineData("dir/../file", false)]
        [InlineData("dir/sub/../../file", false)]
        [InlineData("..\\..\\file", false)]
        [InlineData("normal-file.txt", true)]
        [InlineData("dir/sub/file.txt", true)]
        [InlineData("..../weirdname", true)]
        [InlineData(".hidden/file", true)]
        public void IsSafeArchiveEntry_additional_cases(string entry, bool expected)
        {
            Assert.Equal(expected, SafeFileSystem.IsSafeArchiveEntry(entry));
        }
    }
}
