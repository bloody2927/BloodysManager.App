using System;
using System.IO;
using System.Text.Json;

namespace BloodysManager.App.Services;

public sealed class Config
{
    public string RepositoryUrl { get; set; } =
        "https://github.com/azerothcore/azerothcore-wotlk.git";

    public string DownloadPath { get; set; } = string.Empty;

    public string? RepositoryRef { get; set; }
        = "main";

    public string Language { get; set; } = "en";

    public string LivePath { get; set; } =
        @"B:\\Server\\Live\\azerothcore-wotlk";

    public string CopyPath { get; set; } =
        @"B:\\Server\\Live_Copy\\azerothcore-wotlk-copy";

    public string BackupRoot { get; set; } =
        @"B:\\Server\\Backup";

    public string BackupZip { get; set; } =
        @"B:\\Server\\BackupZip";

    static string CfgFile(string baseDir) => Path.Combine(baseDir, "appsettings.json");

    public static Config Load(string baseDir)
    {
        var cfg = new Config();
        var file = CfgFile(baseDir);
        if (!File.Exists(file))
        {
            return cfg;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        var root = doc.RootElement;

        if (root.TryGetProperty(nameof(RepositoryUrl), out var repoUrl))
            cfg.RepositoryUrl = repoUrl.GetString() ?? string.Empty;
        if (root.TryGetProperty(nameof(DownloadPath), out var dlPath))
            cfg.DownloadPath = dlPath.GetString() ?? string.Empty;
        if (root.TryGetProperty(nameof(RepositoryRef), out var repoRef))
            cfg.RepositoryRef = repoRef.ValueKind == JsonValueKind.Null ? null : repoRef.GetString();
        if (root.TryGetProperty(nameof(Language), out var lang))
            cfg.Language = lang.GetString() ?? cfg.Language;
        if (root.TryGetProperty(nameof(LivePath), out var live))
            cfg.LivePath = live.GetString() ?? cfg.LivePath;
        if (root.TryGetProperty(nameof(CopyPath), out var copy))
            cfg.CopyPath = copy.GetString() ?? cfg.CopyPath;
        if (root.TryGetProperty(nameof(BackupRoot), out var backupRoot))
            cfg.BackupRoot = backupRoot.GetString() ?? cfg.BackupRoot;
        if (root.TryGetProperty(nameof(BackupZip), out var backupZip))
            cfg.BackupZip = backupZip.GetString() ?? cfg.BackupZip;

        return cfg;
    }

    public void Save(string baseDir)
    {
        var file = CfgFile(baseDir);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        using var stream = File.Create(file);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        writer.WriteString(nameof(RepositoryUrl), RepositoryUrl ?? string.Empty);
        writer.WriteString(nameof(DownloadPath), DownloadPath ?? string.Empty);
        writer.WriteString(nameof(RepositoryRef), RepositoryRef ?? string.Empty);
        writer.WriteString(nameof(Language), Language ?? string.Empty);
        writer.WriteString(nameof(LivePath), LivePath ?? string.Empty);
        writer.WriteString(nameof(CopyPath), CopyPath ?? string.Empty);
        writer.WriteString(nameof(BackupRoot), BackupRoot ?? string.Empty);
        writer.WriteString(nameof(BackupZip), BackupZip ?? string.Empty);
        writer.WriteEndObject();
        writer.Flush();
    }

    public static Config Load() => Load(AppContext.BaseDirectory);

    public void Save() => Save(AppContext.BaseDirectory);
}
