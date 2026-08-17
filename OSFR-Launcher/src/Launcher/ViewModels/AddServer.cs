using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

using Launcher.Extensions;
using Launcher.Helpers;
using Launcher.Models;

using NLog;

namespace Launcher.ViewModels;

public partial class AddServer : Popup
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    [Required]
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(AddServer), nameof(ValidateServerUrl))]
    private string serverUrl = string.Empty;

    public AddServer()
    {
        View = new Views.AddServer
        {
            DataContext = this
        };
    }

    public static ValidationResult? ValidateServerUrl(string serverUrl, ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(serverUrl))
            return new ValidationResult(App.GetText("Text.Add_Server.InvalidServerUrl1", "<empty>"));

        serverUrl = serverUrl.Trim();

        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var serverUri))
            return new ValidationResult(App.GetText("Text.Add_Server.InvalidServerUrl1", serverUrl));

        if (serverUri.Scheme != Uri.UriSchemeHttp && serverUri.Scheme != Uri.UriSchemeHttps)
            return new ValidationResult(App.GetText("Text.Add_Server.InvalidServerUrl2", serverUrl));

        return ValidationResult.Success;
    }

    public override Task<bool> ProcessAsync()
    {
        ProgressDescription = App.GetText("Text.Add_Server.Loading");
        return TryAddServerAsync(ServerUrl);
    }

    public static async Task<bool> TryAddServerAsync(string serverUrl)
    {
        try
        {
            serverUrl = serverUrl.Trim();
            var result = await HttpHelper.GetServerManifestAsync(serverUrl);

            if (result.Result != ManifestResult.Success || result.ServerManifest is null)
            {
                App.AddNotification($"""
                                     Could not add the server.
                                     {result.Error}
                                     """, true);
                return false;
            }

            var serverManifest = result.ServerManifest;

            if (string.IsNullOrEmpty(serverManifest.Name))
            {
                App.AddNotification("""
                                    Could not add the server.
                                    Server name is missing in manifest.
                                    """, true);
                return false;
            }

            if (!TryCreateSavePath(serverManifest.Name, out var savePath))
            {
                App.AddNotification("""
                                    Could not add the server.
                                    Failed to create a safe save path for the server.
                                    """, true);
                return false;
            }

            var serverInfo = new ServerInfo
            {
                Url = serverUrl,
                Name = serverManifest.Name,
                Description = serverManifest.Description,
                WebApiUrl = serverManifest.WebApiUrl,
                LoginServer = serverManifest.LoginServer,
                SavePath = savePath
            };

            Settings.Instance.ServerInfoList.Add(serverInfo);
            Settings.Instance.Save();
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "An exception occurred while adding server.");
            App.AddNotification("An error occurred while adding server.", true);
            return false;
        }
    }

    private static bool TryCreateSavePath(string name, out string path)
    {
        path = string.Empty;
        try
        {
            var validName = name.ToValidDirectoryName();
            var basePath = ServerPathHelper.ServersRoot;
            Directory.CreateDirectory(basePath);

            var counter = 1;
            var currentName = validName;

            while (true)
            {
                var candidatePath = ServerPathHelper.GetServerDirectory(currentName);
                if (!Directory.Exists(candidatePath))
                {
                    Directory.CreateDirectory(candidatePath);
                    path = candidatePath;
                    return true;
                }

                currentName = $"{validName}_{counter++}";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to create save path.");
            return false;
        }
    }
}
