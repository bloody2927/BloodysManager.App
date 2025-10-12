using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BloodysManager.App.Models;

namespace BloodysManager.App.Services;

public sealed class CopyService
{
    private readonly Config _cfg;

    public CopyService(Config cfg)
    {
        _cfg = cfg;
    }

    public Task MirrorLiveToCopyAsync(CancellationToken ct)
    {
        return MirrorAsync(_cfg.LivePath, _cfg.CopyPath, ct);
    }

    public Task MirrorLiveToCopyAsync(CancellationToken ct, string? sourceOverride, string? destinationOverride)
    {
        var src = string.IsNullOrWhiteSpace(sourceOverride) ? _cfg.LivePath : sourceOverride;
        var dst = string.IsNullOrWhiteSpace(destinationOverride) ? _cfg.CopyPath : destinationOverride;
        return MirrorAsync(src, dst, ct);
    }

    private static Task MirrorAsync(string? src, string? dst, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(src) || string.IsNullOrWhiteSpace(dst))
        {
            return Task.CompletedTask;
        }

        if (Path.GetFullPath(src) == Path.GetFullPath(dst))
        {
            return Task.CompletedTask;
        }

        if (!Directory.Exists(src))
        {
            return Task.CompletedTask;
        }

        return Task.Run(() =>
        {
            static bool Exclude(string path)
            {
                var normalized = path.Replace('\\', '/');
                if (normalized.Contains("/.git/", StringComparison.OrdinalIgnoreCase)) return true;
                if (normalized.EndsWith("/.git", StringComparison.OrdinalIgnoreCase)) return true;
                if (normalized.EndsWith(".pack", StringComparison.OrdinalIgnoreCase)) return true;
                if (normalized.EndsWith(".idx", StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }

            FileUtil.MirrorTree(src, dst, Exclude, ct);
        }, ct);
    }

    public Task DeleteLiveAsync()
        => DeleteDirectoryAsync(_cfg.LivePath);

    public Task DeleteLiveAsync(string? path)
        => DeleteDirectoryAsync(string.IsNullOrWhiteSpace(path) ? _cfg.LivePath : path);

    public Task DeleteCopyAsync()
        => DeleteDirectoryAsync(_cfg.CopyPath);

    public Task DeleteCopyAsync(string? path)
        => DeleteDirectoryAsync(string.IsNullOrWhiteSpace(path) ? _cfg.CopyPath : path);

    private static Task DeleteDirectoryAsync(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            FileUtil.ForceDeleteDirectory(path);
        }

        return Task.CompletedTask;
    }

    public Task CreateBaseFoldersAsync(string root, ServerProfile? profile = null, CancellationToken ct = default)
    {
        var live = Path.Combine(root, "Live", "azerothcore-wotlk");
        var copy = Path.Combine(root, "Live_Copy", "azerothcore-wotlk-copy");
        var backup = Path.Combine(root, "Backup");
        var backupZip = Path.Combine(root, "BackupZip");

        FileUtil.EnsureDirectory(live, hardenedForCurrentUser: true);
        FileUtil.EnsureDirectory(copy, hardenedForCurrentUser: true);
        FileUtil.EnsureDirectory(backup, hardenedForCurrentUser: true);
        FileUtil.EnsureDirectory(backupZip, hardenedForCurrentUser: true);

        if (profile is not null)
        {
            profile.PathLive = live;
            profile.PathCopy = copy;
            profile.PathBackup = backup;
            profile.PathBackupZip = backupZip;
        }

        _cfg.LivePath = live;
        _cfg.CopyPath = copy;
        _cfg.BackupRoot = backup;
        _cfg.BackupZip = backupZip;

        return Task.CompletedTask;
    }
}
