using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace BloodysManager.App.Services;

public sealed class Localizer : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly Config _cfg;
    private ResourceManager _rm;

    public Localizer(Config cfg)
    {
        _cfg = cfg;
        _rm = new ResourceManager("BloodysManager.App.Resources.Strings", typeof(Localizer).Assembly);
        SetLanguage(_cfg.Language);
    }

    public string this[string key] => _rm.GetString(key) ?? key;

    public string CurrentLanguage { get; private set; } = "de";

    public void SetLanguage(string lang)
    {
        CurrentLanguage = (lang?.ToLowerInvariant() == "en") ? "en" : "de";
        var culture = new CultureInfo(CurrentLanguage);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture   = culture;
        _cfg.Language = CurrentLanguage;
        PropertyChanged?.Invoke(this, new(nameof(CurrentLanguage)));
        PropertyChanged?.Invoke(this, new("")); // force re-bind
    }
}
