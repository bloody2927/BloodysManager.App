using System.IO;
using System.IO.Compression;
using BloodysManager.App.ViewModels;

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

    public async Task<string> RotateAsync(ShellService shell, CopyService copy, ServerProfileVM profile, CancellationToken ct)
    {
        var copyRoot = profile.CopyRoot;
        var copyPath = profile.CopyPath ?? throw new InvalidOperationException("Copy path not configured.");
        var backupRoot = profile.BackupRoot ?? throw new InvalidOperationException("Backup root not configured.");

        if (!Directory.Exists(copyPath))
            await copy.MirrorLiveToCopyAsync(profile, ct);

        var tag = DateTime.Now.ToString("dd_MM_yy");
        string Base(string? suffix = null) => Path.Combine(backupRoot, suffix is null ? $"Backup_{tag}" : $"Backup_{tag}_{suffix}");
        string baseName = Base(); int i = 0;
        while (File.Exists(baseName + ".7z") || File.Exists(baseName + ".rar") || File.Exists(baseName + ".zip"))
            baseName = Base((++i).ToString());

        Directory.CreateDirectory(backupRoot);

        string? seven = FindExe("7z.exe");
        string? rar   = FindExe("rar.exe");
        foreach (var fmt in _cfg.PreferredArchiveOrder.Select(f => f.ToLowerInvariant()))
        {
            if (fmt == "7z" && seven is not null)
            {
                var dst = baseName + ".7z";
                var (c, _, e) = await shell.RunAsync(seven, $"a -t7z -mx=7 -mmt=on \"{dst}\" \"{copyPath}\\*\"", null, ct);
                if (c != 0) throw new Exception($"7z failed: {e}");
                if (Directory.Exists(copyPath))
                    Directory.Delete(copyPath, true);
                await copy.MirrorLiveToCopyAsync(profile, ct); return dst;
            }
            if (fmt == "rar" && rar is not null)
            {
                var dst = baseName + ".rar";
                var (c, _, e) = await shell.RunAsync(rar, $"a -ep1 -m5 -r \"{dst}\" \"{copyPath}\\*\"", null, ct);
                if (c != 0) throw new Exception($"rar failed: {e}");
                if (Directory.Exists(copyPath))
                    Directory.Delete(copyPath, true);
                await copy.MirrorLiveToCopyAsync(profile, ct); return dst;
            }
            if (fmt == "zip")
            {
                var dst = baseName + ".zip";
                ZipFile.CreateFromDirectory(copyPath, dst, CompressionLevel.Optimal, includeBaseDirectory: false);
                if (Directory.Exists(copyPath))
                    Directory.Delete(copyPath, true);
                await copy.MirrorLiveToCopyAsync(profile, ct); return dst;
            }
        }
        throw new Exception("No supported archive format available.");
    }
}
