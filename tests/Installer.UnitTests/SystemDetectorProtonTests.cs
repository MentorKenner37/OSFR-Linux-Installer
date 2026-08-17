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

        private static void WriteMinimalElf(string path, ushort machine, bool littleEndian = true)
        {
            var header = new byte[20];
            header[0] = 0x7F; header[1] = (byte)'E'; header[2] = (byte)'L'; header[3] = (byte)'F';
            header[4] = 2; // 64-bit
            header[5] = littleEndian ? (byte)1 : (byte)2; // endianness
            // bytes 6-17 left zero
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

        [Fact]
        public void Aarch64_proton_named_build_is_incompatible_on_x64_host()
        {
            if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
                return; // test applies to non-ARM hosts

            var tools = _fixture.CreateDir("compat-tools");
            var dir = Path.Combine(tools, "Proton-aarch64-test");
            Directory.CreateDirectory(dir);
            var proton = Path.Combine(dir, "proton");
            File.WriteAllText(proton, "stub");

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

            var wine64 = Path.Combine(dir, "files", "bin", "wine64");
            WriteMinimalElf(wine64, 62, littleEndian: true); // machine 62 == x86_64

            var result = SystemDetector.InspectProtonRuntime(proton);
            Assert.Equal("x86_64", result.RuntimeArchitecture);
            // Compatible depends on host architecture; on CI host (x64) this should be true
            if (RuntimeInformation.OSArchitecture == Architecture.X64)
                Assert.True(result.Compatible);
        }
    }
}
