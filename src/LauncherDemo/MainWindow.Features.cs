using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia.Interactivity;

namespace OSFR.Linux.LauncherDemo;

public partial class MainWindow
{
    private readonly string _launcherStateDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".local", "share", "OSFR-Linux", "LauncherDemo");

    private LauncherPreferences _preferences = new();

    private string PreferencesPath => Path.Combine(_launcherStateDirectory, "preferences.json");

    private void WindowOpened(object? sender, EventArgs e)
    {
        Directory.CreateDirectory(_launcherStateDirectory);
        LoadPreferences();

        if (_preferences.Servers.Count == 0)
        {
            _preferences.Servers.Add("https://opensourcefreerealms.com/");
            SavePreferences();
        }

        RefreshServerList();
        RefreshSettingsPage();
        LoadRememberedForCurrentServer();
        ShowPage(HomePage);
    }

    private void HomeClicked(object? sender, RoutedEventArgs e) => ShowPage(HomePage);

    private void ServersClicked(object? sender, RoutedEventArgs e)
    {
        RefreshServerList();
        ShowPage(ServersPage);
    }

    private void SettingsClicked(object? sender, RoutedEventArgs e)
    {
        RefreshSettingsPage();
        ShowPage(SettingsPage);
    }

    private void AboutClicked(object? sender, RoutedEventArgs e) => ShowPage(AboutPage);

    private void ShowPage(Avalonia.Controls.Control page)
    {
        HomePage.IsVisible = ReferenceEquals(page, HomePage);
        ServersPage.IsVisible = ReferenceEquals(page, ServersPage);
        SettingsPage.IsVisible = ReferenceEquals(page, SettingsPage);
        AboutPage.IsVisible = ReferenceEquals(page, AboutPage);
    }

    private void ConnectRememberClicked(object? sender, RoutedEventArgs e)
    {
        LoadRememberedForCurrentServer();
        ConnectClicked(sender, e);
    }

    private void LaunchRememberClicked(object? sender, RoutedEventArgs e)
    {
        SaveRememberedForCurrentServer();
        LaunchClicked(sender, e);
    }

    private void AddServerClicked(object? sender, RoutedEventArgs e)
    {
        if (!TryNormalizeServerUrl(NewServerUrlBox.Text, out var uri, out var error))
        {
            ServersStatusText.Text = error;
            ServersStatusText.Foreground = Bad;
            return;
        }

        var normalized = uri.AbsoluteUri;
        if (!_preferences.Servers.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            _preferences.Servers.Add(normalized);
            _preferences.Servers.Sort(StringComparer.OrdinalIgnoreCase);
            SavePreferences();
        }

        NewServerUrlBox.Text = string.Empty;
        RefreshServerList();
        ServersStatusText.Text = "Server saved.";
        ServersStatusText.Foreground = Good;
    }

    private void UseServerClicked(object? sender, RoutedEventArgs e)
    {
        if (SavedServersList.SelectedItem is not string selected)
        {
            ServersStatusText.Text = "Select a server first.";
            ServersStatusText.Foreground = Bad;
            return;
        }

        ServerUrlBox.Text = selected;
        LoadRememberedForCurrentServer();
        ShowPage(HomePage);
        ConnectClicked(sender, e);
    }

    private void RemoveServerClicked(object? sender, RoutedEventArgs e)
    {
        if (SavedServersList.SelectedItem is not string selected)
        {
            ServersStatusText.Text = "Select a server first.";
            ServersStatusText.Foreground = Bad;
            return;
        }

        _preferences.Servers.RemoveAll(x => string.Equals(x, selected, StringComparison.OrdinalIgnoreCase));
        _preferences.Profiles.Remove(ProfileKey(selected));
        TryClearSecret(selected);
        SavePreferences();
        RefreshServerList();
        ServersStatusText.Text = "Server removed from the launcher.";
        ServersStatusText.Foreground = Muted;
    }

    private void SaveRememberedForCurrentServer()
    {
        if (!TryNormalizeServerUrl(ServerUrlBox.Text, out var uri, out _))
            return;

        var server = uri.AbsoluteUri;
        var key = ProfileKey(server);

        if (RememberMeCheckBox.IsChecked != true)
        {
            _preferences.Profiles.Remove(key);
            TryClearSecret(server);
            SavePreferences();
            return;
        }

        var username = UsernameBox.Text?.Trim() ?? string.Empty;
        _preferences.Profiles[key] = new RememberedProfile
        {
            ServerUrl = server,
            Username = username,
            Remember = true
        };

        var password = PasswordBox.Text ?? string.Empty;
        var passwordSaved = password.Length == 0 || TryStoreSecret(server, password);
        SavePreferences();

        RememberStatusText.Text = passwordSaved
            ? "Remembered for this server. Password is stored in the Linux secret service."
            : "Username remembered. Install/configure Secret Service support (secret-tool) to remember the password securely.";
        RememberStatusText.Foreground = passwordSaved ? Good : Muted;
    }

    private void LoadRememberedForCurrentServer()
    {
        if (!TryNormalizeServerUrl(ServerUrlBox.Text, out var uri, out _))
            return;

        var server = uri.AbsoluteUri;
        if (!_preferences.Profiles.TryGetValue(ProfileKey(server), out var profile) || !profile.Remember)
        {
            RememberMeCheckBox.IsChecked = false;
            return;
        }

        RememberMeCheckBox.IsChecked = true;
        UsernameBox.Text = profile.Username;

        var password = TryLookupSecret(server);
        if (!string.IsNullOrEmpty(password))
            PasswordBox.Text = password;

        RememberStatusText.Text = string.IsNullOrEmpty(password)
            ? "Username remembered for this server."
            : "Remembered credentials loaded for this server.";
        RememberStatusText.Foreground = Good;
    }

    private void RefreshServerList()
    {
        SavedServersList.ItemsSource = null;
        SavedServersList.ItemsSource = _preferences.Servers.ToArray();
        SavedServerCountText.Text = $"{_preferences.Servers.Count} saved server{(_preferences.Servers.Count == 1 ? string.Empty : "s")}";
    }

    private void RefreshSettingsPage()
    {
        ProtonSettingText.Text = EmptyAsDash(ReadRuntimeConfig("proton-path.txt"));
        SteamSettingText.Text = EmptyAsDash(ReadRuntimeConfig("steam-path.txt"));
        PrefixSettingText.Text = EmptyAsDash(ReadRuntimeConfig("prefix-path.txt"));
        BackendSettingText.Text = EmptyAsDash(ReadRuntimeConfig("graphics-backend.txt"));
        DisplaySettingText.Text = EmptyAsDash(ReadRuntimeConfig("display-mode.txt"));
    }

    private void OpenInstallerFolderClicked(object? sender, RoutedEventArgs e)
    {
        var installDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "OSFR-Linux");
        Directory.CreateDirectory(installDirectory);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "xdg-open",
                ArgumentList = { installDirectory },
                UseShellExecute = false
            });
        }
        catch (Exception ex)
        {
            SettingsStatusText.Text = $"Could not open installer folder: {ex.Message}";
            SettingsStatusText.Foreground = Bad;
        }
    }

    private static string EmptyAsDash(string value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

    private void LoadPreferences()
    {
        try
        {
            if (!File.Exists(PreferencesPath))
                return;

            _preferences = JsonSerializer.Deserialize<LauncherPreferences>(File.ReadAllText(PreferencesPath))
                ?? new LauncherPreferences();
        }
        catch
        {
            _preferences = new LauncherPreferences();
        }
    }

    private void SavePreferences()
    {
        Directory.CreateDirectory(_launcherStateDirectory);
        var json = JsonSerializer.Serialize(_preferences, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(PreferencesPath, json);
    }

    private static string ProfileKey(string serverUrl)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(serverUrl.Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes);
    }

    private static bool SecretToolAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "which",
                ArgumentList = { "secret-tool" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            process?.WaitForExit(1500);
            return process is { ExitCode: 0 };
        }
        catch
        {
            return false;
        }
    }

    private static bool TryStoreSecret(string serverUrl, string password)
    {
        if (!SecretToolAvailable())
            return false;

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "secret-tool",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };
            process.StartInfo.ArgumentList.Add("store");
            process.StartInfo.ArgumentList.Add("--label=Sanctuary Linux Launcher");
            process.StartInfo.ArgumentList.Add("application");
            process.StartInfo.ArgumentList.Add("sanctuary-linux-launcher");
            process.StartInfo.ArgumentList.Add("server");
            process.StartInfo.ArgumentList.Add(serverUrl);
            process.Start();
            process.StandardInput.Write(password);
            process.StandardInput.Close();
            process.WaitForExit(3000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string? TryLookupSecret(string serverUrl)
    {
        if (!SecretToolAvailable())
            return null;

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "secret-tool",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };
            process.StartInfo.ArgumentList.Add("lookup");
            process.StartInfo.ArgumentList.Add("application");
            process.StartInfo.ArgumentList.Add("sanctuary-linux-launcher");
            process.StartInfo.ArgumentList.Add("server");
            process.StartInfo.ArgumentList.Add(serverUrl);
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);
            return process.ExitCode == 0 ? output.TrimEnd('\r', '\n') : null;
        }
        catch
        {
            return null;
        }
    }

    private static void TryClearSecret(string serverUrl)
    {
        if (!SecretToolAvailable())
            return;

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "secret-tool",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };
            process.StartInfo.ArgumentList.Add("clear");
            process.StartInfo.ArgumentList.Add("application");
            process.StartInfo.ArgumentList.Add("sanctuary-linux-launcher");
            process.StartInfo.ArgumentList.Add("server");
            process.StartInfo.ArgumentList.Add(serverUrl);
            process.Start();
            process.WaitForExit(3000);
        }
        catch
        {
            // Credential cleanup should never prevent normal launcher use.
        }
    }

    private sealed class LauncherPreferences
    {
        public List<string> Servers { get; set; } = new();
        public Dictionary<string, RememberedProfile> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class RememberedProfile
    {
        public string ServerUrl { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public bool Remember { get; set; }
    }
}
