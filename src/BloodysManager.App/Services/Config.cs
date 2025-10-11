using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace BloodysManager.App.Services;

public sealed class Config
{
    public string RepoUrl { get; init; } = "";
    public string Branch  { get; init; } = "master";
    public int    Threads { get; init; } = 8;

    public string BaseRoot { get; set; } = @"D:\Server";
    public string[] PreferredArchiveOrder { get; init; } = new[] { "7z", "rar", "zip" };

    public string Language { get; set; } = "de";

    public string LiveRoot   => Path.Combine(BaseRoot, "Live");
    public string LivePath   => Path.Combine(LiveRoot, "azerothcore-wotlk");
    public string CopyRoot   => Path.Combine(BaseRoot, "Live_Copy");
    public string CopyPath   => Path.Combine(CopyRoot, "azerothcore-wotlk-copy");
    public string BackupRoot => Path.Combine(BaseRoot, "Backup");

    public List<ServerProfileConfig> Profiles { get; init; } = new();

    public IReadOnlyList<ServerProfileConfig> ResolveProfiles()
    {
        if (Profiles is { Count: > 0 })
        {
            foreach (var profile in Profiles)
            {
                if (string.IsNullOrWhiteSpace(profile.Name))
                    profile.Name = "Profile";
                if (string.IsNullOrWhiteSpace(profile.LivePath))
                    profile.LivePath = LivePath;
                if (string.IsNullOrWhiteSpace(profile.CopyPath))
                    profile.CopyPath = CopyPath;
                if (string.IsNullOrWhiteSpace(profile.BackupRoot))
                    profile.BackupRoot = BackupRoot;
                if (string.IsNullOrWhiteSpace(profile.BackupZipRoot) && !string.IsNullOrWhiteSpace(profile.BackupRoot))
                    profile.BackupZipRoot = Path.Combine(profile.BackupRoot, "Zip");
            }
            return Profiles;
        }

        var fallback = new ServerProfileConfig
        {
            Name = "Default",
            LivePath = LivePath,
            CopyPath = CopyPath,
            BackupRoot = BackupRoot,
            BackupZipRoot = Path.Combine(BackupRoot, "Zip"),
        };
        return new[] { fallback };
    }

    public static Config Load(string filePath)
    {
        using var fs = File.OpenRead(filePath);
        return JsonSerializer.Deserialize<Config>(fs) ?? new Config();
    }
}
