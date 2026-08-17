using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Launcher.Helpers;
using Launcher.Models;
using Launcher.Services;

using NLog;

namespace Launcher.ViewModels;

public partial class Login : Popup
{
    private readonly Server _server;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    [ObservableProperty]
    private string? warning;

    [Required]
    [ObservableProperty]
    [NotifyDataErrorInfo]
    private string username = string.Empty;

    [Required]
    [ObservableProperty]
    [NotifyDataErrorInfo]
    private string password = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool rememberUsername;

    [ObservableProperty]
    private bool rememberPassword;

    public bool AutoFocusUsername => string.IsNullOrEmpty(Username);
    public bool AutoFocusPassword => !string.IsNullOrEmpty(Username) && string.IsNullOrEmpty(Password);

    public Login(Server server)
    {
        _server = server;
        AddSecureWarning();
        RememberUsername = _server.Info.RememberUsername;
        RememberPassword = _server.Info.RememberPassword;
        Username = RememberUsername ? _server.Info.Username ?? string.Empty : string.Empty;
        MigrateLegacyPassword();
        Password = RememberPassword ? CredentialHelper.GetPassword(_server.Info) ?? string.Empty : string.Empty;

        View = new Views.Login
        {
            DataContext = this
        };
    }

    partial void OnRememberUsernameChanged(bool value)
    {
        _server.Info.RememberUsername = value;
        if (!value)
            _server.Info.Username = null;
        Settings.Instance.Save();
    }

    partial void OnRememberPasswordChanged(bool value)
    {
        _server.Info.RememberPassword = value;
        if (!value)
            CredentialHelper.Clear(_server.Info);
        Settings.Instance.Save();
    }

    [RelayCommand]
    public void Register() => App.ShowPopup(new Register(_server));

    public override async Task<bool> ProcessAsync()
    {
        try
        {
            ProgressDescription = App.GetText("Text.Login.Loading");
            using var httpClient = HttpHelper.CreateHttpClient();
            var loginRequest = new LoginRequest { Username = Username, Password = Password };
            var baseUri = new Uri(_server.Info.WebApiUrl);
            var loginUri = new Uri(baseUri, "login");
            var httpResponse = await httpClient.PostAsJsonAsync(loginUri, loginRequest);

            if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                App.AddNotification(App.GetText("Text.Login.Unauthorized"), true);
                Password = string.Empty;
                return false;
            }

            if (!httpResponse.IsSuccessStatusCode)
            {
                App.AddNotification("Login failed. Please check your username and password and try again", true);
                _logger.Warn("Login failed for server: '{Name}'. API returned {StatusCode}: {Reason}.", _server.Info.Name, httpResponse.StatusCode, httpResponse.ReasonPhrase);
                return false;
            }

            var loginResponse = await httpResponse.Content.ReadFromJsonAsync<LoginResponse>();
            if (loginResponse == null || string.IsNullOrEmpty(loginResponse.SessionId))
            {
                App.AddNotification("Login failed. Please check your username and password and try again.", true);
                _logger.Warn("Invalid login API response from server: '{Name}'. Response body was null or SessionId was missing.", _server.Info.Name);
                return false;
            }

            SaveRememberedCredentials();
            await LaunchClientAsync(loginResponse.SessionId, loginResponse.LaunchArguments);
            return true;
        }
        catch (Exception ex)
        {
            App.AddNotification("Login failed. Please check your username and password and try again", true);
            _logger.Error(ex, "An exception occurred logging into server: '{Name}'.", _server.Info.Name);
            return false;
        }
    }

    private void AddSecureWarning()
    {
        if (Uri.TryCreate(_server.Info.WebApiUrl, UriKind.Absolute, out var webApiUrl)
            && webApiUrl.Scheme != Uri.UriSchemeHttps)
            Warning = App.GetText("Text.Server.SecureApiWarning");
    }

    private void SaveRememberedCredentials()
    {
        _server.Info.Username = RememberUsername && !string.IsNullOrEmpty(Username) ? Username : null;
        if (RememberPassword && !string.IsNullOrEmpty(Password))
            CredentialHelper.SavePassword(_server.Info, Password);
        else
            CredentialHelper.Clear(_server.Info);
        Settings.Instance.Save();
    }

    private void MigrateLegacyPassword()
    {
        var legacyPassword = _server.Info.LegacyPassword;
        if (string.IsNullOrEmpty(legacyPassword))
            return;
        if (_server.Info.RememberPassword)
            CredentialHelper.SavePassword(_server.Info, legacyPassword);
        _server.Info.LegacyPassword = null;
        Settings.Instance.Save();
    }

    private async Task LaunchClientAsync(string sessionId, string? serverArguments)
    {
        if (OperatingSystem.IsWindows() && !Dx9Helper.IsInstalled())
        {
            await NotifyDirectX9MissingAsync();
            return;
        }

        var launcherArguments = new List<string>
        {
            $"Server={_server.Info.LoginServer}",
            $"SessionId={sessionId}",
            $"Internationalization:Locale={Settings.Instance.Locale}"
        };

        if (!string.IsNullOrEmpty(serverArguments))
            launcherArguments.Add(serverArguments);

        var arguments = string.Join(' ', launcherArguments);
        var workingDirectory = Path.Combine(Constants.SavePath, _server.Info.SavePath, "Client");
        var executablePath = Path.Combine(workingDirectory, Constants.ClientExecutableName);

        if (!File.Exists(executablePath))
        {
            App.AddNotification("Unable to launch the game. The executable file could not be found.", true);
            _logger.Error("Client executable not found for server: '{Name}' at path: {Path}.", _server.Info.Name, executablePath);
            return;
        }

        string fileName;
        string processArguments;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var protonPath = WineHelper.GetPath();
            if (string.IsNullOrEmpty(protonPath))
            {
                App.AddNotification("Unable to launch the game, Proton is not installed.", true);
                return;
            }

            fileName = protonPath;
            processArguments = $"run \"{Constants.ClientExecutableName}\" {arguments}";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            fileName = executablePath;
            processArguments = arguments;
        }
        else
        {
            App.AddNotification("Unable to launch the game, your operating system is not supported.", true);
            return;
        }

        _server.Process = new Process();
        _server.Process.StartInfo.WorkingDirectory = workingDirectory;
        _server.Process.StartInfo.FileName = fileName;
        _server.Process.StartInfo.Arguments = processArguments;
        _server.Process.StartInfo.UseShellExecute = false;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var protonPrefix = WineHelper.GetConfiguredPath("prefix-path.txt");
            if (string.IsNullOrWhiteSpace(protonPrefix))
                protonPrefix = Path.Combine(home, ".local", "share", "OSFR-Linux", "ProtonPrefix");

            var steamRoot = WineHelper.GetSteamRoot();
            if (string.IsNullOrWhiteSpace(steamRoot))
            {
                App.AddNotification("Unable to launch the game because the Steam installation path could not be determined.", true);
                return;
            }

            Directory.CreateDirectory(protonPrefix);
            _server.Process.StartInfo.Environment["STEAM_COMPAT_DATA_PATH"] = protonPrefix;
            _server.Process.StartInfo.Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = steamRoot;
            _server.Process.StartInfo.Environment["PROTON_LOG"] = "0";
        }

        _server.Process.EnableRaisingEvents = true;
        _server.Process.Exited += _server.ClientProcessExited;

        try
        {
            _logger.Info("Launching FreeRealms through Proton: {Proton}", fileName);
            _logger.Info("Proton arguments: {Arguments}", processArguments);
            _server.Process.Start();
        }
        catch (Exception ex)
        {
            App.AddNotification("An error occurred while launching the game. Please try again.", true);
            _logger.Error(ex, "Failed to start the client process for server: {Name}.", _server.Info.Name);
            _server.Process?.Dispose();
            _server.Process = null;
        }
    }

    private async Task NotifyDirectX9MissingAsync()
    {
        App.AddNotification("Unable to launch the game, DirectX 9 is not available.", true);
        await Task.Delay(500);

        try
        {
            var window = App.GetWindow();
            await window.Launcher.LaunchUriAsync(new Uri(Constants.DirectXDownloadUrl));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open the DirectX download page automatically.");
            App.AddNotification($"""
                                 Could not open the DirectX download page.
                                 Please open this URL manually: {Constants.DirectXDownloadUrl}
                                 """, true);
        }
    }
}
