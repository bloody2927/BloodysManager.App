using System;
using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace BloodysManager.App.Services;

public sealed class Localizer : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly Config _cfg;
    private readonly ResourceManager _resourceManager;

    public Localizer(Config cfg)
    {
        _cfg = cfg;
        _resourceManager = new ResourceManager("BloodysManager.App.Resources.Strings", typeof(Localizer).Assembly);
        SetLanguage(_cfg.Language);
    }

    public string this[string key]
    {
        get
        {
            try
            {
                var value = _resourceManager.GetString(key);
                return string.IsNullOrEmpty(value) ? key : value;
            }
            catch (MissingManifestResourceException)
            {
                return key;
            }
        }
    }

    public string CurrentLanguage { get; private set; } = "en";

    public void SetLanguage(string lang)
    {
        if (string.IsNullOrWhiteSpace(lang) || lang.Equals("en", StringComparison.OrdinalIgnoreCase))
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            CurrentLanguage = "en";
        }
        else
        {
            var culture = new CultureInfo(lang);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CurrentLanguage = culture.Name;
        }

        PropertyChanged?.Invoke(this, new(nameof(CurrentLanguage)));
        PropertyChanged?.Invoke(this, new("Item[]"));
    }
}
