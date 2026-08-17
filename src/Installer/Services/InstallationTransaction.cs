using System.Text.Json;

namespace OSFR.Linux.Installer.Services;

internal sealed class InstallationTransaction
{
    private const string StateFileName = ".sanctuary-install-transaction.json";
    private const string BackupDirectoryName = ".launcher-backup";
    private const string OwnershipBackupName = ".ownership-backup";
    private const string LegacyInfoBackupName = ".install-info-backup";
    private const int FormatVersion = 2;

    private sealed record TransactionDocument(
        int Version,
        string State,
        bool HadLauncher,
        bool HadOwnershipMarker,
        bool HadLegacyInfo,
        DateTimeOffset UpdatedUtc);

    private readonly string _installRoot;
    private readonly string _launcherDirectory;
    private readonly string _backupDirectory;
    private readonly string _ownershipMarker;
    private readonly string _ownershipBackup;
    private readonly string _legacyInfo;
    private readonly string _legacyInfoBackup;
    private readonly string _stateFile;
    private bool _hadLauncher;
    private bool _hadOwnershipMarker;
    private bool _hadLegacyInfo;
    private bool _promoted;

    public InstallationTransaction(string installRoot)
    {
        _installRoot = InstallService.NormalizeInstallRoot(installRoot);
        _launcherDirectory = Path.Combine(_installRoot, "Launcher");
        _backupDirectory = Path.Combine(_installRoot, BackupDirectoryName);
        _ownershipMarker = Path.Combine(_installRoot, InstallationOwnership.MarkerFileName);
        _ownershipBackup = Path.Combine(_installRoot, OwnershipBackupName);
        _legacyInfo = Path.Combine(_installRoot, InstallationOwnership.LegacyInstallInfoFileName);
        _legacyInfoBackup = Path.Combine(_installRoot, LegacyInfoBackupName);
        _stateFile = Path.Combine(_installRoot, StateFileName);
    }

    public static void RecoverIfNeeded(string installRoot)
    {
        installRoot = InstallService.NormalizeInstallRoot(installRoot);
        var stateFile = Path.Combine(installRoot, StateFileName);
        if (!File.Exists(stateFile))
            return;

        var launcher = Path.Combine(installRoot, "Launcher");
        var backup = Path.Combine(installRoot, BackupDirectoryName);
        var marker = Path.Combine(installRoot, InstallationOwnership.MarkerFileName);
        var markerBackup = Path.Combine(installRoot, OwnershipBackupName);
        var legacyInfo = Path.Combine(installRoot, InstallationOwnership.LegacyInstallInfoFileName);
        var legacyInfoBackup = Path.Combine(installRoot, LegacyInfoBackupName);

        SafeFileSystem.RefuseSymbolicLink(stateFile, "installation transaction state file");
        SafeFileSystem.RefuseSymbolicLink(launcher, "launcher directory");
        SafeFileSystem.RefuseSymbolicLink(backup, "launcher backup directory");
        SafeFileSystem.RefuseSymbolicLink(marker, "installation ownership marker");
        SafeFileSystem.RefuseSymbolicLink(markerBackup, "ownership backup file");
        SafeFileSystem.RefuseSymbolicLink(legacyInfo, "legacy install-info file");
        SafeFileSystem.RefuseSymbolicLink(legacyInfoBackup, "legacy install-info backup file");

        var document = ReadState(stateFile);
        InstallerLog.Warn($"Recovering interrupted Sanctuary installation transaction in {installRoot}; state={document.State}.");

        if (string.Equals(document.State, "committed", StringComparison.Ordinal))
        {
            CleanupBackup(backup, markerBackup, legacyInfoBackup);
            File.Delete(stateFile);
            return;
        }

        if (document.HadLauncher)
        {
            if (Directory.Exists(backup))
            {
                if (Directory.Exists(launcher))
                    SafeFileSystem.DeleteDirectoryTreeNoFollow(launcher);
                Directory.Move(backup, launcher);
            }
            // If the backup does not exist, the crash happened before the old launcher was moved.
            // Leave the existing launcher untouched.
        }
        else if (Directory.Exists(launcher))
        {
            SafeFileSystem.DeleteDirectoryTreeNoFollow(launcher);
        }

        RestoreRecordedFile(document.HadOwnershipMarker, markerBackup, marker);
        RestoreRecordedFile(document.HadLegacyInfo, legacyInfoBackup, legacyInfo);
        File.Delete(stateFile);
    }

    public void Begin()
    {
        Directory.CreateDirectory(_installRoot);
        SafeFileSystem.RefuseSymbolicLinkAncestors(_installRoot, "installation root");
        SafeFileSystem.RefuseSymbolicLink(_installRoot, "installation root");
        SafeFileSystem.RefuseSymbolicLink(_launcherDirectory, "launcher directory");
        SafeFileSystem.RefuseSymbolicLink(_backupDirectory, "launcher backup directory");
        SafeFileSystem.RefuseSymbolicLink(_ownershipMarker, "installation ownership marker");
        SafeFileSystem.RefuseSymbolicLink(_ownershipBackup, "ownership backup file");
        SafeFileSystem.RefuseSymbolicLink(_legacyInfo, "legacy install-info file");
        SafeFileSystem.RefuseSymbolicLink(_legacyInfoBackup, "legacy install-info backup file");

        if (Directory.Exists(_backupDirectory) || File.Exists(_ownershipBackup) || File.Exists(_legacyInfoBackup))
            throw new InvalidOperationException("Stale Sanctuary transaction backup data exists. Re-run the installer to recover it before continuing.");

        _hadLauncher = Directory.Exists(_launcherDirectory);
        _hadOwnershipMarker = File.Exists(_ownershipMarker);
        _hadLegacyInfo = File.Exists(_legacyInfo);

        WriteState("preparing");

        if (_hadLauncher)
            Directory.Move(_launcherDirectory, _backupDirectory);
        if (_hadOwnershipMarker)
            File.Move(_ownershipMarker, _ownershipBackup);
        if (_hadLegacyInfo)
            File.Move(_legacyInfo, _legacyInfoBackup);

        WriteState("active");
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
        CleanupBackup(_backupDirectory, _ownershipBackup, _legacyInfoBackup);
        File.Delete(_stateFile);
    }

    public void Rollback()
    {
        try
        {
            if (_hadLauncher)
            {
                if (Directory.Exists(_backupDirectory))
                {
                    if (Directory.Exists(_launcherDirectory))
                        SafeFileSystem.DeleteDirectoryTreeNoFollow(_launcherDirectory);
                    Directory.Move(_backupDirectory, _launcherDirectory);
                }
            }
            else if (Directory.Exists(_launcherDirectory))
            {
                SafeFileSystem.DeleteDirectoryTreeNoFollow(_launcherDirectory);
            }

            RestoreRecordedFile(_hadOwnershipMarker, _ownershipBackup, _ownershipMarker);
            RestoreRecordedFile(_hadLegacyInfo, _legacyInfoBackup, _legacyInfo);

            if (File.Exists(_stateFile))
                File.Delete(_stateFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            InstallerLog.Error("Failed to fully roll back Sanctuary installation transaction", ex);
            throw;
        }
    }

    private void WriteState(string state)
    {
        var document = new TransactionDocument(
            FormatVersion,
            state,
            _hadLauncher,
            _hadOwnershipMarker,
            _hadLegacyInfo,
            DateTimeOffset.UtcNow);
        WriteJsonAtomically(_stateFile, document);
    }

    private static TransactionDocument ReadState(string stateFile)
    {
        try
        {
            var document = JsonSerializer.Deserialize<TransactionDocument>(File.ReadAllText(stateFile));
            if (document is null || document.Version != FormatVersion)
                throw new InvalidDataException("The interrupted-install transaction metadata is invalid.");
            if (document.State is not ("preparing" or "active" or "committed"))
                throw new InvalidDataException("The interrupted-install transaction state is invalid.");
            return document;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The interrupted-install transaction metadata is corrupt.", ex);
        }
    }

    private static void CleanupBackup(string launcherBackup, string markerBackup, string legacyInfoBackup)
    {
        if (Directory.Exists(launcherBackup))
            SafeFileSystem.DeleteDirectoryTreeNoFollow(launcherBackup);
        if (File.Exists(markerBackup))
            File.Delete(markerBackup);
        if (File.Exists(legacyInfoBackup))
            File.Delete(legacyInfoBackup);
    }

    private static void RestoreRecordedFile(bool existedBefore, string backup, string destination)
    {
        if (existedBefore)
        {
            if (File.Exists(backup))
            {
                if (File.Exists(destination))
                    File.Delete(destination);
                File.Move(backup, destination);
            }
            // If no backup exists, the original file was never moved. Leave it untouched.
        }
        else
        {
            if (File.Exists(destination))
                File.Delete(destination);
            if (File.Exists(backup))
                File.Delete(backup);
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