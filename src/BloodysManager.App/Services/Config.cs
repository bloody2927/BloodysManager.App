using System.IO;
using System.Text.Json;

namespace BloodysManager.App.Services;

public sealed class Config
{
    public string RepositoryUrl { get; set; } =
        "https://github.com/azerothcore/azerothcore-wotlk.git";

    public string LivePath { get; set; } =
        @"B:\\Server\\Live\\azerothcore-wotlk";

    public string CopyPath { get; set; } =
        @"B:\\Server\\Live_Copy\\azerothcore-wotlk-copy";

    public string BackupRoot { get; set; } =
        @"B:\\Server\\Backup";

    static string CfgFile(string baseDir) => Path.Combine(baseDir, "appsettings.json");

    public static Config Load(string baseDir)
    {
        var file = CfgFile(baseDir);
        if (!File.Exists(file)) return new Config();
        return JsonSerializer.Deserialize<Config>(File.ReadAllText(file)) ?? new Config();
    }

    public void Save(string baseDir)
    {
        var file = CfgFile(baseDir);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
