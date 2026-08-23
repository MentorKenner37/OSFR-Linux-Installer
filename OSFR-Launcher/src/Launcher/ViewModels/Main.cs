using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Collections;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Launcher.Helpers;
using Launcher.Models;
using Launcher.Services;

using NLog;

namespace Launcher.ViewModels;

public partial class Main : ObservableObject
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    [ObservableProperty]
    private Popup? popup;

    [ObservableProperty]
    private Server? activeServer;

    [ObservableProperty]
    private string version = App.CurrentVersion;

    public AvaloniaList<Server> Servers { get; } = [];
    public AvaloniaList<Notification> Notifications { get; } = [];

    public Main()
    {
#if DEBUG && DESIGNMODE
        if (Avalonia.Controls.Design.IsDesignMode)
        {
            Servers.Clear();

            for (var i = 0; i < 20; i++)
                Servers.Add(new Server());
        }
#endif

        Settings.Instance.ServerInfoList.CollectionChanged += ServerInfoList_CollectionChanged;
        Settings.Instance.DiscordActivityChanged += (_, _) => UpdateDiscordActivity();
    }

    private void ServerInfoList_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewStartingIndex != -1)
        {
            var serverInfo = Settings.Instance.ServerInfoList[e.NewStartingIndex];
            Servers.Add(new Server(serverInfo, this));
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldStartingIndex != -1)
        {
            Servers.RemoveAt(e.OldStartingIndex);

            if (ActiveServer != null && !Servers.Contains(ActiveServer))
                ActiveServer = null;
        }
    }

    public void OnLoad()
    {
        foreach (var serverInfo in Settings.Instance.ServerInfoList)
            Servers.Add(new Server(serverInfo, this));

        if (Settings.Instance.ServerInfoList.Count == 0)
        {
            _logger.Info("No servers found in settings. Adding default servers.");

            foreach (var defaultServerUrl in Constants.DefaultServerUrls)
                _ = AddServer.TryAddServerAsync(defaultServerUrl);
        }

        UpdateDiscordActivity();
        _ = UpdateService.CheckAsync(false);
    }

    public void UpdateDiscordActivity()
    {
        if (!Settings.Instance.DiscordActivity)
            return;

        var serversPlaying = Servers.Where(x => x.Process is not null).Select(x => x.Info.Name);
        var playingOn = string.Join(", ", serversPlaying);

        var details = string.IsNullOrEmpty(playingOn)
            ? App.GetText("Text.Discord.Idle")
            : App.GetText("Text.Discord.Playing");

        DiscordService.UpdateActivity(details, playingOn);
    }

    [RelayCommand]
    public void ShowSettings()
    {
        var window = App.GetWindow();
        var dialog = new Views.Settings();
        dialog.ShowDialog(window);
    }

    [RelayCommand]
    public void ShowAddServer() => App.ShowPopup(new AddServer());

    [RelayCommand]
    public async Task OpenLogsAsync()
    {
        bool result;

        try
        {
            var window = App.GetWindow();
            var directoryInfo = new DirectoryInfo(Constants.LogsDirectory);
            result = await window.Launcher.LaunchDirectoryInfoAsync(directoryInfo);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error opening logs directory.");
            result = false;
        }

        if (!result)
            App.AddNotification("Unable to open logs directory.", true);
    }

    [RelayCommand]
    public void DeleteServer()
    {
        if (ActiveServer == null)
            return;

        if (ActiveServer.IsDownloading)
        {
            App.AddNotification("Cannot delete server while download is in progress.", true);
            return;
        }

        App.ShowPopup(new DeleteServer(ActiveServer.Info));
    }

    public void OnReceiveNotification(Notification notification)
    {
        if (Notifications.Count >= 3)
            Notifications.RemoveAt(0);

        Notifications.Add(notification);

        DispatcherTimer.RunOnce(
            () => Notifications.Remove(notification),
            TimeSpan.FromSeconds(3));
    }
}
