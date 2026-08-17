using System;
using System.Diagnostics;
using System.Reflection;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using Launcher.Models;
using Launcher.ViewModels;

using NLog;

namespace Launcher;

public partial class App : Application
{
    private readonly Logger _logger;

    private Main _main = null!;
    private Window _window = null!;

    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    public App()
    {
        _logger = LogManager.GetCurrentClassLogger();
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime applicationLifetime)
            return;

        var main = new Views.Main();

        _main = main.ViewModel;
        _window = main;

        applicationLifetime.MainWindow = main;

        main.Show();

        base.OnFrameworkInitializationCompleted();
    }

    public static Window GetWindow()
    {
        if (Current is not App app)
            throw new InvalidOperationException();

        return app._window;
    }

    public static string GetText(string key, params object?[] args)
    {
        if (Current is not App)
            throw new InvalidOperationException();

        if (Current.FindResource(key) is not string text)
            return $"#{key}";

        return string.Format(text, args);
    }

    public static void AddNotification(string message, bool isError = false)
    {
        if (Current is not App app)
            throw new InvalidOperationException();

        var notice = new Notification
        {
            IsError = isError,
            Message = message
        };

        app._logger.Log(isError ? LogLevel.Error : LogLevel.Info, message);
        app._main.OnReceiveNotification(notice);
    }

    public static void ShowPopup(Popup popup, bool process = false)
    {
        if (Current is not App app)
            throw new InvalidOperationException();

        if (app._main.Popup?.InProgress ?? false)
            return;

        app._main.Popup = popup;

        if (process)
            ProcessPopup();
    }

    public static async void ProcessPopup()
    {
        if (Current is not App app)
            throw new InvalidOperationException();

        try
        {
            if (app._main.Popup is null)
                return;

            if (!app._main.Popup.Validate())
                return;

            app._main.Popup.InProgress = true;

            var finished = await app._main.Popup.ProcessAsync();

            if (finished)
                app._main.Popup = null;
            else
                app._main.Popup.InProgress = false;
        }
        catch (Exception ex)
        {
            // Fire-and-forget popup processing must not crash the launcher.
            Debug.WriteLine(ex);
        }
    }

    public static void CancelPopup()
    {
        if (Current is not App app)
            throw new InvalidOperationException();

        if (app._main.Popup?.InProgress ?? true)
            return;

        app._main.Popup = null;
    }
}
