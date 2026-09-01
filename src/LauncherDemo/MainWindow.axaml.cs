using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace OSFR.Linux.LauncherDemo;

public partial class MainWindow : Window
{
    private const int MaxManifestBytes = 256 * 1024;
    private const int MaxLogoBytes = 2 * 1024 * 1024;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    private static readonly IBrush Good = new SolidColorBrush(Color.Parse("#55D27E"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#A0A0A0"));
    private static readonly IBrush Bad = new SolidColorBrush(Color.Parse("#E06A6A"));

    private readonly HttpClient _httpClient = new()
    {
        Timeout = RequestTimeout
    };

    public MainWindow()
    {
        InitializeComponent();
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Sanctuary-Linux-Launcher-Demo", "0.1"));
    }

    private async void ConnectClicked(object? sender, RoutedEventArgs e)
    {
        if (!TryNormalizeServerUrl(ServerUrlBox.Text, out var serverBaseUri, out var error))
        {
            SetConnectionState(error, false);
            return;
        }

        ConnectButton.IsEnabled = false;
        LaunchButton.IsEnabled = false;
        OnlineText.Text = "CONNECTING";
        OnlineText.Foreground = Muted;
        ConnectionStatusText.Text = "Fetching servermanifest.xml…";
        ConnectionStatusText.Foreground = Muted;

        try
        {
            var manifestUri = new Uri(serverBaseUri, "servermanifest.xml");
            var xml = await DownloadTextLimitedAsync(manifestUri, MaxManifestBytes);
            var manifest = ParseManifest(xml);

            ServerNameText.Text = manifest.Name;
            ServerDescriptionText.Text = manifest.Description;
            ManifestVersionText.Text = $"Manifest: v{manifest.Version} (v2 compatible)";
            WebApiText.Text = manifest.WebApiUrl;
            LoginServerText.Text = manifest.LoginServer;

            var loginReachable = await TestLoginServerAsync(manifest.LoginServer);
            if (loginReachable)
            {
                OnlineText.Text = "ONLINE";
                OnlineText.Foreground = Good;
                ClientStatusText.Text = "Manifest loaded and login server accepted a TCP connection.";
                SetConnectionState($"Connected to {manifest.Name}.", true);
                LaunchButton.IsEnabled = true;
            }
            else
            {
                OnlineText.Text = "PARTIAL";
                OnlineText.Foreground = Bad;
                ClientStatusText.Text = "Manifest loaded, but the login server did not accept a connection.";
                ConnectionStatusText.Text = "Server metadata is reachable; login endpoint could not be verified.";
                ConnectionStatusText.Foreground = Bad;
            }

            await TryLoadServerLogoAsync(serverBaseUri, manifest.LogoUrl);
        }
        catch (Exception ex)
        {
            OnlineText.Text = "OFFLINE";
            OnlineText.Foreground = Bad;
            SetConnectionState(ex.Message, false);
            ClientStatusText.Text = "Connection failed before the server could be verified.";
        }
        finally
        {
            ConnectButton.IsEnabled = true;
        }
    }

    private static bool TryNormalizeServerUrl(string? value, out Uri baseUri, out string error)
    {
        baseUri = null!;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Enter a server URL.";
            return false;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var parsed))
        {
            error = "Server URL is not a valid absolute URL.";
            return false;
        }

        if (parsed.Scheme != Uri.UriSchemeHttps)
        {
            error = "This demo requires HTTPS server URLs.";
            return false;
        }

        var text = parsed.AbsoluteUri.EndsWith('/') ? parsed.AbsoluteUri : parsed.AbsoluteUri + "/";
        baseUri = new Uri(text);
        return true;
    }

    private async Task<string> DownloadTextLimitedAsync(Uri uri, int maxBytes)
    {
        using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is not null &&
            !mediaType.Equals("application/xml", StringComparison.OrdinalIgnoreCase) &&
            !mediaType.Equals("text/xml", StringComparison.OrdinalIgnoreCase) &&
            !mediaType.Equals("text/plain", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Manifest returned unsupported content type: {mediaType}");
        }

        if (response.Content.Headers.ContentLength is > maxBytes)
            throw new InvalidDataException("Server manifest is too large.");

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var memory = new MemoryStream();
        var buffer = new byte[16 * 1024];
        var total = 0;

        while (true)
        {
            var read = await stream.ReadAsync(buffer);
            if (read == 0)
                break;

            total += read;
            if (total > maxBytes)
                throw new InvalidDataException("Server manifest exceeded the size limit.");

            memory.Write(buffer, 0, read);
        }

        return System.Text.Encoding.UTF8.GetString(memory.ToArray());
    }

    private static ServerManifest ParseManifest(string xml)
    {
        var document = XDocument.Parse(xml, LoadOptions.None);
        var root = document.Root ?? throw new InvalidDataException("Manifest has no root element.");

        if (!root.Name.LocalName.Equals("ServerManifest", StringComparison.Ordinal))
            throw new InvalidDataException("Document is not a Sanctuary server manifest.");

        if (!int.TryParse(root.Attribute("version")?.Value, out var version))
            throw new InvalidDataException("Manifest version is missing or invalid.");

        if (version is < 1 or > 2)
            throw new InvalidDataException($"Unsupported server manifest version: {version}. This demo supports v1 and v2.");

        string Required(string name) =>
            root.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value.Trim() is { Length: > 0 } value
                ? value
                : throw new InvalidDataException($"Manifest is missing required field {name}.");

        string? Optional(string name) =>
            root.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value.Trim() is { Length: > 0 } value
                ? value
                : null;

        return new ServerManifest(
            version,
            Required("Name"),
            Required("Description"),
            Required("WebApiUrl"),
            Required("LoginServer"),
            Optional("LogoUrl"));
    }

    private static async Task<bool> TestLoginServerAsync(string loginServer)
    {
        if (!TryParseHostPort(loginServer, out var host, out var port))
            return false;

        using var client = new TcpClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        try
        {
            await client.ConnectAsync(host, port, timeout.Token);
            return client.Connected;
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException or ArgumentException)
        {
            return false;
        }
    }

    private static bool TryParseHostPort(string value, out string host, out int port)
    {
        host = string.Empty;
        port = 0;

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Port > 0)
        {
            host = uri.Host;
            port = uri.Port;
            return true;
        }

        var split = value.LastIndexOf(':');
        if (split <= 0 || split == value.Length - 1)
            return false;

        host = value[..split].Trim().Trim('[', ']');
        return host.Length > 0 && int.TryParse(value[(split + 1)..], out port) && port is > 0 and <= 65535;
    }

    private async Task TryLoadServerLogoAsync(Uri serverBaseUri, string? logoUrl)
    {
        ServerLogoImage.Source = null;
        ServerLogoImage.IsVisible = false;
        ServerLogoFallback.IsVisible = true;

        var candidates = new List<Uri>();
        if (!string.IsNullOrWhiteSpace(logoUrl) && Uri.TryCreate(serverBaseUri, logoUrl, out var manifestLogo))
        {
            if (manifestLogo.Scheme == Uri.UriSchemeHttps)
                candidates.Add(manifestLogo);
        }

        candidates.Add(new Uri(serverBaseUri, "servericon.png"));

        foreach (var candidate in candidates.Distinct())
        {
            try
            {
                using var response = await _httpClient.GetAsync(candidate, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                    continue;

                var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                if (!mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (response.Content.Headers.ContentLength is > MaxLogoBytes)
                    continue;

                await using var source = await response.Content.ReadAsStreamAsync();
                using var bounded = new MemoryStream();
                var buffer = new byte[16 * 1024];
                var total = 0;
                while (true)
                {
                    var read = await source.ReadAsync(buffer);
                    if (read == 0)
                        break;
                    total += read;
                    if (total > MaxLogoBytes)
                        throw new InvalidDataException("Server logo is too large.");
                    bounded.Write(buffer, 0, read);
                }

                bounded.Position = 0;
                ServerLogoImage.Source = new Bitmap(bounded);
                ServerLogoImage.IsVisible = true;
                ServerLogoFallback.IsVisible = false;
                return;
            }
            catch
            {
                // Branding is optional. Connectivity must not fail because a logo is absent or malformed.
            }
        }
    }

    private void SetConnectionState(string message, bool success)
    {
        ConnectionStatusText.Text = message;
        ConnectionStatusText.Foreground = success ? Good : Bad;
    }

    private sealed record ServerManifest(
        int Version,
        string Name,
        string Description,
        string WebApiUrl,
        string LoginServer,
        string? LogoUrl);
}
