using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using BloodysManager.App.Models;

namespace BloodysManager.App.Services;

public sealed class ConfigService
{
    private readonly string _filePath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true
    };

    public AppConfig Load()
    {
        if (!File.Exists(_filePath))
        {
            var cfg = CreateDefault();
            Save(cfg);
            return cfg;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var cfg = JsonSerializer.Deserialize<AppConfig>(json, _options) ?? CreateDefault();
            EnsureProfiles(cfg);
            return cfg;
        }
        catch
        {
            var cfg = CreateDefault();
            Save(cfg);
            return cfg;
        }
    }

    public void Save(AppConfig cfg)
    {
        EnsureProfiles(cfg);
        var json = JsonSerializer.Serialize(cfg, _options);
        File.WriteAllText(_filePath, json);
    }

    private static AppConfig CreateDefault()
    {
        var cfg = new AppConfig();
        EnsureProfiles(cfg);
        return cfg;
    }

    private static void EnsureProfiles(AppConfig cfg)
    {
        if (cfg.Profiles is null)
            cfg.Profiles = new ObservableCollection<ServerProfile>();
        if (cfg.Profiles.Count == 0)
            cfg.Profiles.Add(new ServerProfile { Name = "Server 1" });
        cfg.SelectedProfileIndex = Math.Clamp(cfg.SelectedProfileIndex, 0, cfg.Profiles.Count - 1);
    }
}
