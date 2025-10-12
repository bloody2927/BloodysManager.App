using System.ComponentModel;
using System.Resources;

namespace BloodysManager.App.Services;

public sealed class Localizer : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    readonly Config _cfg;
    readonly ResourceManager _rm;

    public Localizer(Config cfg)
    {
        _cfg = cfg;
        _rm = new ResourceManager("BloodysManager.App.Resources.Strings", typeof(Localizer).Assembly);
        CurrentLanguage = "en";
    }

    public string this[string key] => _rm.GetString(key) ?? key;

    public string CurrentLanguage { get; private set; } = "en";

    // no-op now (we keep signature for minimal impact)
    public void SetLanguage(string lang)
    {
        CurrentLanguage = "en";
        PropertyChanged?.Invoke(this, new(nameof(CurrentLanguage)));
    }
}
