using System.IO;
using System.IO.Compression;

namespace BloodysManager.App.Services;

public sealed class BackupService
{
    private readonly Config _cfg;
    public BackupService(Config cfg){ _cfg = cfg; }

    string? FindExe(string name)
    {
        string[] common = {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip", "7z.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WinRAR", "Rar.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "WinRAR", "Rar.exe"),
        };
        var fromPath = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator)
            .Select(p => Path.Combine(p, name))
            .FirstOrDefault(File.Exists);
        return fromPath ?? common.FirstOrDefault(File.Exists);
    }

    public async Task<string> RotateAsync(ShellService shell, CopyService copy, CancellationToken ct)
    {
        if (!Directory.Exists(_cfg.CopyRoot))
            await copy.MirrorLiveToCopyAsync(ct);

        var tag = DateTime.Now.ToString("dd_MM_yy");
        string Base(string? suffix = null) => Path.Combine(_cfg.BackupRoot, suffix is null ? $"Backup_{tag}" : $"Backup_{tag}_{suffix}");
        string baseName = Base(); int i = 0;
        while (File.Exists(baseName + ".7z") || File.Exists(baseName + ".rar") || File.Exists(baseName + ".zip"))
            baseName = Base((++i).ToString());

        Directory.CreateDirectory(_cfg.BackupRoot);

        string? seven = FindExe("7z.exe");
        string? rar   = FindExe("rar.exe");
        foreach (var fmt in _cfg.PreferredArchiveOrder.Select(f => f.ToLowerInvariant()))
        {
            if (fmt == "7z" && seven is not null)
            {
                var dst = baseName + ".7z";
                var (c, _, e) = await shell.RunAsync(seven, $"a -t7z -mx=7 -mmt=on \"{dst}\" \"{_cfg.CopyRoot}\\*\"", null, ct);
                if (c != 0) throw new Exception($"7z failed: {e}");
                Directory.Delete(_cfg.CopyRoot, true); await copy.MirrorLiveToCopyAsync(ct); return dst;
            }
            if (fmt == "rar" && rar is not null)
            {
                var dst = baseName + ".rar";
                var (c, _, e) = await shell.RunAsync(rar, $"a -ep1 -m5 -r \"{dst}\" \"{_cfg.CopyRoot}\\*\"", null, ct);
                if (c != 0) throw new Exception($"rar failed: {e}");
                Directory.Delete(_cfg.CopyRoot, true); await copy.MirrorLiveToCopyAsync(ct); return dst;
            }
            if (fmt == "zip")
            {
                var dst = baseName + ".zip";
                ZipFile.CreateFromDirectory(_cfg.CopyRoot, dst, CompressionLevel.Optimal, includeBaseDirectory: false);
                Directory.Delete(_cfg.CopyRoot, true); await copy.MirrorLiveToCopyAsync(ct); return dst;
            }
        }
        throw new Exception("No supported archive format available.");
    }
}
