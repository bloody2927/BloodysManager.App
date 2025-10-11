using System.Text.Json;
using System.IO;

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

    public static Config Load(string filePath)
    {
        using var fs = File.OpenRead(filePath);
        return JsonSerializer.Deserialize<Config>(fs) ?? new Config();
    }
}
