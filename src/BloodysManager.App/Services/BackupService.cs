using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

    public Task<string> RotateAsync(ShellService shell, CopyService copy, CancellationToken ct)
        => RotateAsync(shell, copy, _cfg.CopyPath, _cfg.BackupRoot, _cfg.BackupZip, ct);

    public async Task<string> RotateAsync(
        ShellService shell,
        CopyService copy,
        string? copyPathOverride,
        string? backupRootOverride,
        string? backupZipOverride,
        CancellationToken ct)
    {
        var copyPath = copyPathOverride ?? _cfg.CopyPath;
        var backupRoot = backupRootOverride ?? _cfg.BackupRoot;
        var archiveRoot = backupZipOverride ?? backupRoot;

        if (string.IsNullOrWhiteSpace(copyPath) || string.IsNullOrWhiteSpace(archiveRoot))
            throw new InvalidOperationException("Paths not configured.");

        if (!Directory.Exists(copyPath))
            await copy.MirrorLiveToCopyAsync(ct, _cfg.LivePath, copyPath);

        var tag = DateTime.Now.ToString("dd_MM_yy");
        string Base(string? suffix = null) => Path.Combine(archiveRoot, suffix is null ? $"Backup_{tag}" : $"Backup_{tag}_{suffix}");
        string baseName = Base(); int i = 0;
        while (File.Exists(baseName + ".7z") || File.Exists(baseName + ".rar") || File.Exists(baseName + ".zip"))
            baseName = Base((++i).ToString());

        Directory.CreateDirectory(archiveRoot);

        string? seven = FindExe("7z.exe");
        string? rar   = FindExe("rar.exe");
        foreach (var fmt in new[]{"7z", "rar", "zip"})
        {
            if (fmt == "7z" && seven is not null)
            {
                var dst = baseName + ".7z";
                var (c, _, e) = await shell.RunAsync(seven, $"a -t7z -mx=7 -mmt=on \"{dst}\" \"{copyPath}\\*\"", null, ct);
                if (c != 0) throw new Exception($"7z failed: {e}");
                await copy.MirrorLiveToCopyAsync(ct, _cfg.LivePath, copyPath);
                return dst;
            }
            if (fmt == "rar" && rar is not null)
            {
                var dst = baseName + ".rar";
                var (c, _, e) = await shell.RunAsync(rar, $"a -ep1 -m5 -r \"{dst}\" \"{copyPath}\\*\"", null, ct);
                if (c != 0) throw new Exception($"rar failed: {e}");
                await copy.MirrorLiveToCopyAsync(ct, _cfg.LivePath, copyPath);
                return dst;
            }
            if (fmt == "zip")
            {
                var dst = baseName + ".zip";
                ZipFile.CreateFromDirectory(copyPath, dst, CompressionLevel.Optimal, includeBaseDirectory: false);
                await copy.MirrorLiveToCopyAsync(ct, _cfg.LivePath, copyPath);
                return dst;
            }
        }
        throw new Exception("No supported archive format available.");
    }
}
