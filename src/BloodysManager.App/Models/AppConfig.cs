using System.Collections.ObjectModel;

namespace BloodysManager.App.Models;

public class AppConfig
{
    public string RepositoryUrl { get; set; } = "https://github.com/azerothcore/azerothcore-wotlk.git";
    public string DownloadTarget { get; set; } = @"B:\\Downloads";
    public ObservableCollection<ServerProfile> Profiles { get; set; } = new();
    public int SelectedProfileIndex { get; set; }
    public string WorldExePath { get; set; } = string.Empty;
    public string AuthExePath { get; set; } = string.Empty;
}
