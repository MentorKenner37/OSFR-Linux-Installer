using System;
using System.IO;
using System.Runtime.InteropServices;
using OSFR.Linux.Installer.Services;
using Xunit;

namespace Installer.UnitTests
{
    public class SystemDetectorProtonTests : IDisposable
    {
        private readonly TempDirFixture _fixture = new();
        public void Dispose() => _fixture.Dispose();

        [Fact]
        public void FindExecutable_UsesProvidedPath()
        {
            var executable = Path.Combine(_fixture.Root, "curl");
            File.WriteAllText(executable, "stub");

            Assert.Equal(executable, SystemDetector.FindExecutable("curl", _fixture.Root));
            Assert.Null(SystemDetector.FindExecutable("missing-command", _fixture.Root));
            Assert.Throws<ArgumentException>(() => SystemDetector.FindExecutable("../curl", _fixture.Root));
        }

        private static void WriteMinimalElf(string path, ushort machine, bool littleEndian = true)
        {
            var header = new byte[20];
            header[0] = 0x7F; header[1] = (byte)'E'; header[2] = (byte)'L'; header[3] = (byte)'F';
            header[4] = 2;
            header[5] = littleEndian ? (byte)1 : (byte)2;
            if (littleEndian)
            {
                header[18] = (byte)(machine & 0xFF);
                header[19] = (byte)((machine >> 8) & 0xFF);
            }
            else
            {
                header[18] = (byte)((machine >> 8) & 0xFF);
                header[19] = (byte)(machine & 0xFF);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, header);
        }

        private static void MakeExecutable(string path)
        {
            if (!OperatingSystem.IsLinux())
                return;

            File.SetUnixFileMode(path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        [Fact]
        public void Aarch64_proton_named_build_is_incompatible_on_x64_host()
        {
            if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
                return;

            var tools = _fixture.CreateDir("compat-tools");
            var dir = Path.Combine(tools, "Proton-aarch64-test");
            Directory.CreateDirectory(dir);
            var proton = Path.Combine(dir, "proton");
            File.WriteAllText(proton, "stub");
            MakeExecutable(proton);

            var result = SystemDetector.InspectProtonRuntime(proton);
            Assert.Equal("aarch64", result.RuntimeArchitecture);
            Assert.False(result.Compatible);
        }

        [Fact]
        public void Proton_detects_x86_64_runtime_from_elf_wine64()
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
                return;

            var dir = _fixture.CreateDir("proton-x64");
            var proton = Path.Combine(dir, "proton");
            File.WriteAllText(proton, "stub");
            MakeExecutable(proton);

            var wine64 = Path.Combine(dir, "files", "bin", "wine64");
            WriteMinimalElf(wine64, 62, littleEndian: true);

            var result = SystemDetector.InspectProtonRuntime(proton);
            Assert.Equal("x86_64", result.RuntimeArchitecture);
            if (RuntimeInformation.OSArchitecture == Architecture.X64)
                Assert.True(result.Compatible);
        }
    }
}
