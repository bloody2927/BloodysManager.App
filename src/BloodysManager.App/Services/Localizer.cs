using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace BloodysManager.App.Services;

public sealed class Localizer : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly Config _cfg;
    private readonly ResourceManager _rm;
    public string CurrentLanguage { get; private set; } = "en";

    public Localizer(Config cfg)
    {
        _cfg = cfg;

        // BaseName MUST match the manifest: <DefaultNamespace>.Resources.Strings
        _rm = new ResourceManager("BloodysManager.App.Resources.Strings", typeof(Localizer).Assembly);

        SetLanguage(cfg.Language);
    }

    public string this[string key]
    {
        get
        {
            try
            {
                var s = _rm.GetString(key, CultureInfo.CurrentUICulture);
                return string.IsNullOrEmpty(s) ? key : s!;
            }
            catch
            {
                return key; // safe fallback
            }
        }
    }

    public void SetLanguage(string lang)
    {
        // Normalize (e.g., "de", "en-US", etc.)
        var culture = new CultureInfo(string.IsNullOrWhiteSpace(lang) ? "en" : lang);

        // Set both UI culture (resource lookup) and culture (formatting)
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture   = culture;

        CurrentLanguage = culture.Name;
        _cfg.Language   = culture.Name;

        // notify UI to re-bind all indexer values
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}
