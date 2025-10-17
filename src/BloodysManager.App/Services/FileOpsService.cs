using System;
using System.IO;
using System.Linq;

namespace BloodysManager.App.Services;

public sealed class FileOpsService
{
    public void EnsureDir(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    public void CopyDirectoryContents(string src, string dst, global::System.Action<string>? log = null)
    {
        if (!Directory.Exists(src))
            throw new DirectoryNotFoundException($"Source directory not found: {src}");

        EnsureDir(dst);
        foreach (var file in Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(src, file);
            var target = Path.Combine(dst, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
            log?.Invoke($"[Copy] {relative}");
        }
    }

    public void DeleteDirectoryContents(string path, global::System.Action<string>? log = null)
    {
        if (!Directory.Exists(path))
            return;

        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly))
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
            log?.Invoke($"[Delete] {Path.GetFileName(file)}");
        }

        foreach (var dir in Directory.EnumerateDirectories(path))
        {
            Directory.Delete(dir, recursive: true);
            log?.Invoke($"[DeleteDir] {Path.GetFileName(dir)}");
        }
    }

    public string SnapshotToBackup(string livePath, string backupPath, global::System.Action<string>? log = null)
    {
        if (!Directory.Exists(livePath))
            throw new DirectoryNotFoundException($"Live path not found: {livePath}");

        EnsureDir(backupPath);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var target = Path.Combine(backupPath, $"snapshot_{stamp}");
        Directory.CreateDirectory(target);
        CopyDirectoryContents(livePath, target, log);
        log?.Invoke($"[Backup] Snapshot created: {target}");
        return target;
    }

    public void RotateBackups(string backupPath, int keep = 5, global::System.Action<string>? log = null)
    {
        if (!Directory.Exists(backupPath))
            return;

        var directories = Directory.GetDirectories(backupPath)
            .OrderByDescending(d => d)
            .ToList();

        if (directories.Count <= keep)
            return;

        foreach (var old in directories.Skip(keep))
        {
            Directory.Delete(old, recursive: true);
            log?.Invoke($"[Rotate] Removed {Path.GetFileName(old)}");
        }
    }
}
