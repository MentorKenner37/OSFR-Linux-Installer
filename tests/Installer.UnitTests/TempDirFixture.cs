using System;
using System.IO;

namespace Installer.UnitTests
{
    public sealed class TempDirFixture : IDisposable
    {
        public string Root { get; }

        public TempDirFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "sanctuary-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string CreateDir(string relative)
        {
            var p = Path.Combine(Root, relative);
            Directory.CreateDirectory(p);
            return p;
        }

        public string CreateFile(string relative, string content)
        {
            var path = Path.Combine(Root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); } catch { }
        }
    }
}
