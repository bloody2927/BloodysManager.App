using System.IO;
using System.Threading;

namespace BloodysManager.App.Services;

public sealed class CopyService
{
    readonly ShellService _sh;
    readonly Config _cfg;

    public CopyService(ShellService sh, Config cfg)
    { _sh = sh; _cfg = cfg; }

    public Task CreateBaseFoldersAsync(string root, Models.ServerProfile? profile = null, CancellationToken ct = default)
    {
        // root\Live, root\Live_Copy, root\Backup, root\BackupZip
        var live     = Path.Combine(root, "Live", "azerothcore-wotlk");
        var copy     = Path.Combine(root, "Live_Copy", "azerothcore-wotlk-copy");
        var backup   = Path.Combine(root, "Backup");
        var backup7z = Path.Combine(root, "BackupZip");

        Directory.CreateDirectory(live);
        Directory.CreateDirectory(copy);
        Directory.CreateDirectory(backup);
        Directory.CreateDirectory(backup7z);

        if (profile is not null)
        {
            profile.PathLive = live;
            profile.PathCopy = copy;
            profile.PathBackup = backup;
            profile.PathBackupZip = backup7z;
        }

        // write back to config so the UI uses the chosen root
        _cfg.LivePath   = live;
        _cfg.CopyPath   = copy;
        _cfg.BackupRoot = backup;
        _cfg.BackupZip  = backup7z;

        return Task.CompletedTask;
    }

    public Task MirrorLiveToCopyAsync(CancellationToken ct = default)
        => MirrorAsync(_cfg.LivePath, _cfg.CopyPath, ct);

    public Task MirrorLiveToCopyAsync(CancellationToken ct, string? livePath, string? copyPath)
        => MirrorAsync(livePath ?? _cfg.LivePath, copyPath ?? _cfg.CopyPath, ct);

    public Task DeleteLiveAsync()
        => DeleteDirSafeAsync(_cfg.LivePath);

    public Task DeleteLiveAsync(string? path)
        => DeleteDirSafeAsync(path ?? _cfg.LivePath);

    public Task DeleteCopyAsync()
        => DeleteDirSafeAsync(_cfg.CopyPath);

    public Task DeleteCopyAsync(string? path)
        => DeleteDirSafeAsync(path ?? _cfg.CopyPath);

    static async Task MirrorAsync(string? src, string? dst, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(src) || string.IsNullOrWhiteSpace(dst)) return;
        if (!Directory.Exists(src)) return;
        Directory.CreateDirectory(dst);

        // copy directories (excluding .git)
        foreach (var dir in Directory.EnumerateDirectories(src, "*", SearchOption.AllDirectories))
        {
            if (dir.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar)) continue;
            var rel = Path.GetRelativePath(src, dir);
            Directory.CreateDirectory(Path.Combine(dst, rel));
        }

        // copy files (excluding anything inside .git)
        foreach (var file in Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories))
        {
            if (file.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar)) continue;

            var rel = Path.GetRelativePath(src, file);
            var target = Path.Combine(dst, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            // remove readonly if exists
            try
            {
                if (File.Exists(target))
                {
                    var attrs = File.GetAttributes(target);
                    if ((attrs & FileAttributes.ReadOnly) != 0)
                        File.SetAttributes(target, attrs & ~FileAttributes.ReadOnly);
                }
                File.Copy(file, target, overwrite: true);
            }
            catch
            {
                // best-effort: skip locked files
            }

            ct.ThrowIfCancellationRequested();
        }

        await Task.CompletedTask;
    }

    static Task DeleteDirSafeAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return Task.CompletedTask;

        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            try
            {
                var attrs = File.GetAttributes(file);
                if ((attrs & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(file, attrs & ~FileAttributes.ReadOnly);
                File.Delete(file);
            }
            catch { /* ignore */ }
        }

        try { Directory.Delete(path, recursive: true); } catch { /* ignore */ }
        return Task.CompletedTask;
    }
}
