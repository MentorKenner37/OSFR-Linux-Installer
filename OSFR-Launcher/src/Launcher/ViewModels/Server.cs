using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Media;
using Avalonia.Platform.Storage;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HashDepot;

using Launcher.Helpers;
using Launcher.Models;

using LiveMarkdown.Avalonia;

using NLog;

namespace Launcher.ViewModels;

public partial class Server : ObservableObject
{
    private readonly Main _main = null!;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private static readonly SolidColorBrush WhiteBrush = new(Colors.White);
    private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(35, 165, 90));
    private static readonly SolidColorBrush YellowBrush = new(Color.FromRgb(204, 204, 0));
    private static readonly SolidColorBrush RedBrush = new(Color.FromRgb(242, 63, 67));

    [ObservableProperty]
    private ServerInfo info = null!;

    [ObservableProperty]
    private bool isEnabled;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private string status = App.GetText("Text.ServerStatus.Offline");

    [ObservableProperty]
    private int onlinePlayers;

    [ObservableProperty]
    private bool isOnline;

    [ObservableProperty]
    private Process? process;

    [ObservableProperty]
    private IBrush? serverStatusFill = WhiteBrush;

    [ObservableProperty]
    private bool isDownloading;

    [ObservableProperty]
    private ObservableStringBuilder markdownBuilder = new();

    public Server()
    {
#if DEBUG && DESIGNMODE
        if (Avalonia.Controls.Design.IsDesignMode)
        {
            var faker = new Bogus.Faker();

            Info = new ServerInfo
            {
                Url = "https://example.com",
                Name = $"{faker.Name.FirstName()}'s Server",
                Description = faker.Lorem.Paragraphs(5),
                SavePath = "Name",
                LoginServer = "127.0.0.1:20042",
                WebApiUrl = "https://example.com"
            };
        }
#endif
    }

    public Server(ServerInfo info, Main main)
    {
        Info = info;
        _main = main;
    }

    public async Task OnShowAsync()
    {
        MarkdownBuilder.Clear();
        MarkdownBuilder.Append(Info.Description);
        await RefreshCommand.ExecuteAsync(null);
    }

    public void ClientProcessExited(object? sender, EventArgs e)
    {
        Process?.Dispose();
        Process = null;
    }

    [RelayCommand]
    private async Task OpenUriAsync(LinkClickedEventArgs args)
    {
        if (args.HRef is { IsAbsoluteUri: true, Scheme: "http" or "https" } url)
        {
            var window = App.GetWindow();
            await window.Launcher.LaunchUriAsync(url);
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task RefreshAsync()
    {
        Status = App.GetText("Text.ServerStatus.Refreshing");
        ServerStatusFill = YellowBrush;

        if (!string.IsNullOrEmpty(Info.Url))
        {
            try
            {
                var result = await HttpHelper.GetServerManifestAsync(Info.Url);

                if (result.Result != ManifestResult.Success || result.ServerManifest is null)
                {
                    App.AddNotification($"""
                                         Failed to get server info.
                                         {result.Error}
                                         """, true);

                    _logger.Error("Failed to get server manifest for: {Url}: {Error}.", Info.Url, result.Error);

                    switch (result.Result)
                    {
                        case ManifestResult.UnsupportedVersion:
                            ServerStatusFill = RedBrush;
                            Status = App.GetText("Text.ServerStatus.UnsupportedVersion");
                            break;
                        default:
                            ServerStatusFill = RedBrush;
                            Status = App.GetText("Text.ServerStatus.Offline");
                            break;
                    }

                    IsEnabled = false;
                    return;
                }

                var serverManifest = result.ServerManifest;
                Info.Name = serverManifest.Name;
                Info.Description = serverManifest.Description;
                Info.WebApiUrl = serverManifest.WebApiUrl;
                Info.LoginServer = serverManifest.LoginServer;
                Settings.Instance.Save();
            }
            catch (Exception ex)
            {
                ServerStatusFill = RedBrush;
                Status = App.GetText("Text.ServerStatus.Offline");
                App.AddNotification("An error occurred while getting server info.", true);
                _logger.Error(ex, "An exception was thrown while getting server info for: {Url}.", Info.Url);
                IsEnabled = false;
                return;
            }
        }

        try
        {
            var serverStatus = await ServerStatusHelper.GetAsync(Info.LoginServer);
            IsOnline = serverStatus.IsOnline;

            if (serverStatus.IsOnline)
            {
                Status = App.GetText(serverStatus.IsLocked
                    ? "Text.ServerStatus.Locked"
                    : "Text.ServerStatus.Online");
                OnlinePlayers = serverStatus.OnlinePlayers;
                ServerStatusFill = serverStatus.IsLocked ? RedBrush : GreenBrush;
            }
            else
            {
                Status = App.GetText("Text.ServerStatus.Offline");
                OnlinePlayers = 0;
                ServerStatusFill = RedBrush;
            }
        }
        catch (Exception ex)
        {
            ServerStatusFill = RedBrush;
            Status = App.GetText("Text.ServerStatus.Offline");
            _logger.Error(ex, "Error refreshing server status for: '{Name}'.", Info.Name);
            App.AddNotification("Unable to refresh server status.", true);
        }

        IsEnabled = true;
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task PlayAsync()
    {
        if (Process != null)
        {
            App.AddNotification("Unable to launch, the game is already open.", true);
            _logger.Warn("Unable to launch, the game is already open for server: '{Name}'.", Info.Name);
            return;
        }

        if (!string.IsNullOrEmpty(Info.Url))
        {
            var clientManifest = await GetClientManifestAsync();
            if (clientManifest is null)
                return;

            StatusMessage = App.GetText("Text.Server.VerifyClientFiles");
            if (!await VerifyClientFilesAsync(clientManifest))
            {
                StatusMessage = string.Empty;
                return;
            }

            if (!clientManifest.Languages.Contains(Settings.Instance.Locale))
            {
                StatusMessage = string.Empty;
                var selectedLanguage = Locale.LocaleMap[Settings.Instance.Locale];
                var supportedLanguages = clientManifest.Languages.Select(l => Locale.LocaleMap[l]);

                App.AddNotification($"""
                                     The selected language "{selectedLanguage}" is not supported by this server.
                                     Please choose a supported language and try again.
                                     Supported languages:
                                     {string.Join(Environment.NewLine, supportedLanguages)}
                                     """, true);
                return;
            }
        }

        if (!IsOnline)
        {
            StatusMessage = string.Empty;
            App.AddNotification("Unable to login, the server is offline.", true);
            return;
        }

        StatusMessage = string.Empty;
        App.ShowPopup(new Login(this));
    }

    [RelayCommand]
    public async Task OpenFolderAsync()
    {
        bool result;

        try
        {
            var window = App.GetWindow();
            var folderPath = ServerPathHelper.GetServerDirectory(Info.SavePath);
            var directoryInfo = new DirectoryInfo(folderPath);
            result = await window.Launcher.LaunchDirectoryInfoAsync(directoryInfo);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error opening client folder directory.");
            result = false;
        }

        if (!result)
            App.AddNotification("Unable to open server directory.", true);
    }

    private async Task<ClientManifest?> GetClientManifestAsync()
    {
        if (string.IsNullOrEmpty(Info.Url))
            return null;

        try
        {
            var result = await HttpHelper.GetClientManifestAsync(Info.Url);

            if (result.Result != ManifestResult.Success || result.ClientManifest is null)
            {
                App.AddNotification($"""
                                     Failed to get client info.
                                     {result.Error}
                                     """, true);
                _logger.Error("Failed to get client manifest for: {Url}: {Error}.", Info.Url, result.Error);
                return null;
            }

            return result.ClientManifest;
        }
        catch (Exception ex)
        {
            App.AddNotification("An error occurred while getting client info.", true);
            _logger.Error(ex, "An exception was thrown while getting client info for: {Url}.", Info.Url);
        }

        return null;
    }

    private async Task<bool> VerifyClientFilesAsync(ClientManifest clientManifest)
    {
        _logger.Info("Starting verifying client files for: {Name}.", Info.Name);

        List<LocalFile> filesToDownload;
        try
        {
            filesToDownload = await GetFilesToDownloadAsync(clientManifest.RootFolder);
        }
        catch (InvalidDataException ex)
        {
            _logger.Error(ex, "Rejected unsafe client manifest for server: {Name}.", Info.Name);
            App.AddNotification("The server's client manifest contains an unsafe file path and was rejected.", true);
            return false;
        }

        if (filesToDownload.Count == 0)
        {
            _logger.Info("All client files are up to date.");
            return true;
        }

        IsDownloading = true;
        var failedFiles = new ConcurrentBag<string>();

        try
        {
            var filesDownloaded = 0;

            if (Settings.Instance.ParallelDownload)
            {
                var numParallelDownloads = Math.Max(2, Settings.Instance.DownloadThreads);
                var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = numParallelDownloads };
                await Parallel.ForEachAsync(filesToDownload, parallelOptions, async (file, ct) =>
                {
                    if (!await DownloadFileAsync(file, ct))
                        failedFiles.Add(file.Name);

                    filesDownloaded = Interlocked.Increment(ref filesDownloaded);
                    StatusMessage = App.GetText("Text.Server.PreparingGameFiles", filesDownloaded, filesToDownload.Count);
                });
            }
            else
            {
                foreach (var file in filesToDownload)
                {
                    if (!await DownloadFileAsync(file, CancellationToken.None))
                        failedFiles.Add(file.Name);

                    filesDownloaded++;
                    StatusMessage = App.GetText("Text.Server.PreparingGameFiles", filesDownloaded, filesToDownload.Count);
                }
            }
        }
        finally
        {
            IsDownloading = false;
        }

        if (!failedFiles.IsEmpty)
        {
            var message = new StringBuilder();
            message.AppendLine($"Failed to download {failedFiles.Count} file(s):");
            message.AppendLine(string.Join("\n", failedFiles.Take(10)));

            if (failedFiles.Count > 10)
                message.AppendLine($"...And {failedFiles.Count - 10} more.");

            App.AddNotification(message.ToString(), true);
        }

        _logger.Info("Finished verifying client files for: {Name}.", Info.Name);
        return failedFiles.IsEmpty;
    }

    private async Task<bool> DownloadFileAsync(LocalFile file, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(Info.Url))
            return false;

        var downloadFilePath = Path.Combine(file.Path, file.Name);
        string? temporaryPath = null;

        try
        {
            var filePath = ServerPathHelper.GetClientFilePath(Info.SavePath, file.Path, file.Name);
            var fileDirectory = Path.GetDirectoryName(filePath)
                ?? throw new InvalidDataException("The client manifest contains an invalid file path.");
            var clientFileUri = UriHelper.JoinUriPaths(Info.Url, "client", file.Path, file.Name);

            Directory.CreateDirectory(fileDirectory);
            using var response = await HttpHelper.DownloadHttpClient.GetAsync(
                clientFileUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength is long contentLength && contentLength != file.Size)
            {
                _logger.Error(
                    "Server reported an unexpected client file size for {Path}: expected {Expected}, received {Actual}.",
                    downloadFilePath,
                    file.Size,
                    contentLength);
                return false;
            }

            temporaryPath = Path.Combine(fileDirectory, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.download");
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var writeStream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                long total = 0;
                while (true)
                {
                    var read = await source.ReadAsync(buffer, cancellationToken);
                    if (read == 0)
                        break;
                    total += read;
                    if (total > file.Size)
                        throw new InvalidDataException($"Downloaded client file exceeded its declared size: {downloadFilePath}");
                    await writeStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }

            await using (var verifyStream = File.OpenRead(temporaryPath))
            {
                if (verifyStream.Length != file.Size || XXHash.Hash64(verifyStream) != file.Hash)
                {
                    _logger.Error("Downloaded client file failed size/hash verification: {Path}.", downloadFilePath);
                    return false;
                }
            }

            File.Move(temporaryPath, filePath, overwrite: true);
            temporaryPath = null;
            return true;
        }
        catch (InvalidDataException ex)
        {
            _logger.Error(ex, "Rejected unsafe client file path: {Path}.", downloadFilePath);
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error downloading: {Path}.", downloadFilePath);
            return false;
        }
        finally
        {
            if (temporaryPath is not null && File.Exists(temporaryPath))
            {
                try { File.Delete(temporaryPath); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.Warn(ex, "Could not remove temporary download: {Path}.", temporaryPath);
                }
            }
        }
    }

    private async Task<List<LocalFile>> GetFilesToDownloadAsync(ClientFolder rootFolder, string path = "")
    {
        var results = new List<LocalFile>();
        var fileDirectory = ServerPathHelper.GetClientDirectory(Info.SavePath, path);

        foreach (var folder in rootFolder.Folders)
        {
            var folderPath = Path.Combine(path, folder.Name);
            ServerPathHelper.GetClientDirectory(Info.SavePath, folderPath);
            var folderResults = await GetFilesToDownloadAsync(folder, folderPath);
            results.AddRange(folderResults);
        }

        foreach (var file in rootFolder.Files)
        {
            var filePath = ServerPathHelper.GetClientFilePath(Info.SavePath, path, file.Name);

            if (File.Exists(filePath))
            {
                try
                {
                    await using var readStream = File.OpenRead(filePath);

                    if (file.Size == readStream.Length)
                    {
                        var hash = await Task.Run(() => XXHash.Hash64(readStream));
                        if (file.Hash == hash)
                            continue;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Could not verify hash for file: {Path}.", filePath);
                }
            }

            results.Add(new LocalFile
            {
                Path = path,
                Name = file.Name,
                Size = file.Size,
                Hash = file.Hash
            });
        }

        return results;
    }

    partial void OnProcessChanged(Process? value)
    {
        _main.UpdateDiscordActivity();
    }
}
