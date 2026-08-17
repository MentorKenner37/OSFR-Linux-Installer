using System.Text.Json;

namespace OSFR.Linux.Installer.Services;

internal sealed class InstallationTransaction
{
    private const string StateFileName = ".sanctuary-install-transaction.json";
    private const string BackupDirectoryName = ".launcher-backup";
    private const int FormatVersion = 1;

    private sealed record TransactionDocument(int Version, string State, DateTimeOffset UpdatedUtc);

    private readonly string _installRoot;
    private readonly string _launcherDirectory;
    private readonly string _backupDirectory;
    private readonly string _stateFile;
    private bool _promoted;

    public InstallationTransaction(string installRoot)
    {
        _installRoot = InstallService.NormalizeInstallRoot(installRoot);
        _launcherDirectory = Path.Combine(_installRoot, "Launcher");
        _backupDirectory = Path.Combine(_installRoot, BackupDirectoryName);
        _stateFile = Path.Combine(_installRoot, StateFileName);
    }

    public static void RecoverIfNeeded(string installRoot)
    {
        installRoot = InstallService.NormalizeInstallRoot(installRoot);
        var stateFile = Path.Combine(installRoot, StateFileName);
        var launcher = Path.Combine(installRoot, "Launcher");
        var backup = Path.Combine(installRoot, BackupDirectoryName);

        if (!File.Exists(stateFile))
            return;

        SafeFileSystem.RefuseSymbolicLink(stateFile, "installation transaction state file");
        SafeFileSystem.RefuseSymbolicLink(launcher, "launcher directory");
        SafeFileSystem.RefuseSymbolicLink(backup, "launcher backup directory");

        var state = ReadState(stateFile);
        InstallerLog.Warn($"Recovering interrupted Sanctuary installation transaction in {installRoot}; state={state}.");

        if (string.Equals(state, "committed", StringComparison.Ordinal))
        {
            if (Directory.Exists(backup))
                SafeFileSystem.DeleteDirectoryTreeNoFollow(backup);
            File.Delete(stateFile);
            return;
        }

        if (Directory.Exists(launcher))
            SafeFileSystem.DeleteDirectoryTreeNoFollow(launcher);

        if (Directory.Exists(backup))
            Directory.Move(backup, launcher);

        File.Delete(stateFile);
    }

    public void Begin()
    {
        Directory.CreateDirectory(_installRoot);
        SafeFileSystem.RefuseSymbolicLinkAncestors(_installRoot, "installation root");
        SafeFileSystem.RefuseSymbolicLink(_installRoot, "installation root");
        SafeFileSystem.RefuseSymbolicLink(_launcherDirectory, "launcher directory");
        SafeFileSystem.RefuseSymbolicLink(_backupDirectory, "launcher backup directory");

        if (Directory.Exists(_backupDirectory))
            throw new InvalidOperationException("A stale Sanctuary launcher backup exists. Re-run the installer to recover it before continuing.");

        WriteState("active");

        if (Directory.Exists(_launcherDirectory))
            Directory.Move(_launcherDirectory, _backupDirectory);
    }

    public void Promote(string stagedLauncherDirectory)
    {
        if (_promoted)
            throw new InvalidOperationException("The staged launcher has already been promoted.");
        if (Directory.Exists(_launcherDirectory))
            throw new InvalidOperationException("The launcher destination unexpectedly exists during transaction promotion.");

        SafeFileSystem.RefuseSymbolicLink(stagedLauncherDirectory, "staged launcher directory");
        Directory.Move(stagedLauncherDirectory, _launcherDirectory);
        _promoted = true;
    }

    public void Commit()
    {
        WriteState("committed");
        if (Directory.Exists(_backupDirectory))
            SafeFileSystem.DeleteDirectoryTreeNoFollow(_backupDirectory);
        File.Delete(_stateFile);
    }

    public void Rollback()
    {
        try
        {
            if (Directory.Exists(_launcherDirectory))
                SafeFileSystem.DeleteDirectoryTreeNoFollow(_launcherDirectory);
            if (Directory.Exists(_backupDirectory))
                Directory.Move(_backupDirectory, _launcherDirectory);
            if (File.Exists(_stateFile))
                File.Delete(_stateFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            InstallerLog.Error("Failed to fully roll back Sanctuary launcher transaction", ex);
            throw;
        }
    }

    private void WriteState(string state)
    {
        var document = new TransactionDocument(FormatVersion, state, DateTimeOffset.UtcNow);
        WriteJsonAtomically(_stateFile, document);
    }

    private static string ReadState(string stateFile)
    {
        try
        {
            var document = JsonSerializer.Deserialize<TransactionDocument>(File.ReadAllText(stateFile));
            if (document is null || document.Version != FormatVersion)
                throw new InvalidDataException("The interrupted-install transaction metadata is invalid.");
            return document.State;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The interrupted-install transaction metadata is corrupt.", ex);
        }
    }

    private static void WriteJsonAtomically<T>(string destination, T value)
    {
        var directory = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(directory);
        SafeFileSystem.RefuseSymbolicLink(destination, "installation transaction state file");

        var temp = Path.Combine(directory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            if (OperatingSystem.IsLinux())
                File.SetUnixFileMode(temp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.Move(temp, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }
}