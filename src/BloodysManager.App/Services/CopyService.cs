using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BloodysManager.App.Models; // <- stellt ServerProfile bereit

namespace BloodysManager.App.Services;

public sealed class CopyService
{
    private static void EnsureDir(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    public Task MirrorLiveToCopyAsync(ServerProfile sp, IProgress<string>? log, CancellationToken ct)
    {
        if (sp is null)
            throw new ArgumentNullException(nameof(sp));

        return MirrorAsync(sp.PathLive, sp.PathCopy, log, ct);
    }

    public Task MirrorLiveToCopyAsync(string livePath, string copyPath, IProgress<string>? log, CancellationToken ct)
        => MirrorAsync(livePath, copyPath, log, ct);

    public async Task BackupFromLiveAsync(ServerProfile sp, IProgress<string>? log, CancellationToken ct)
    {
        if (sp is null)
            throw new ArgumentNullException(nameof(sp));

        await MirrorAsync(sp.PathLive, sp.PathBackup, log, ct);
    }

    public async Task RotateBackupAsync(ServerProfile sp, int keep = 5, IProgress<string>? log = null, CancellationToken ct = default)
    {
        if (sp is null)
            throw new ArgumentNullException(nameof(sp));

        await Task.Run(() =>
        {
            if (!Directory.Exists(sp.PathBackup))
            {
                log?.Report("ℹ backup folder does not exist — nothing to rotate.");
                return;
            }

            var entries = new DirectoryInfo(sp.PathBackup)
                .EnumerateDirectories()
                .OrderByDescending(d => d.CreationTimeUtc)
                .ToList();

            for (int i = keep; i < entries.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    log?.Report($"[rotate] removing {entries[i].FullName}");
                    entries[i].Delete(true);
                }
                catch (Exception ex)
                {
                    log?.Report($"[rotate] remove failed: {ex.Message}");
                }
            }
        }, ct);
    }

    public Task DeleteContentsAsync(string folder, IProgress<string>? log, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            if (!Directory.Exists(folder))
            {
                log?.Report($"ℹ folder not found: {folder}");
                return;
            }

            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
                log?.Report($"[delete] {Path.GetFileName(file)}");
            }

            foreach (var directory in Directory.EnumerateDirectories(folder, "*", SearchOption.AllDirectories).Reverse())
            {
                ct.ThrowIfCancellationRequested();
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory, false);
                }
            }
        }, ct);
    }

    private static Task MirrorAsync(string source, string destination, IProgress<string>? log, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(source))
                throw new ArgumentException("Source path is empty.", nameof(source));
            if (string.IsNullOrWhiteSpace(destination))
                throw new ArgumentException("Destination path is empty.", nameof(destination));
            if (!Directory.Exists(source))
                throw new DirectoryNotFoundException($"Source directory not found: {source}");

            EnsureDir(destination);

            foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(source, directory);
                Directory.CreateDirectory(Path.Combine(destination, relative));
            }

            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(source, file);
                var target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: true);
                log?.Report($"[copy] {relative}");
            }

            // Delete files that no longer exist in source
            foreach (var targetFile in Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(destination, targetFile);
                var sourceFile = Path.Combine(source, relative);
                if (!File.Exists(sourceFile))
                {
                    File.SetAttributes(targetFile, FileAttributes.Normal);
                    File.Delete(targetFile);
                    log?.Report($"[delete] {relative}");
                }
            }

            foreach (var targetDir in Directory.EnumerateDirectories(destination, "*", SearchOption.AllDirectories)
                         .OrderByDescending(d => d.Length))
            {
                ct.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(destination, targetDir);
                var sourceDir = Path.Combine(source, relative);
                if (!Directory.Exists(sourceDir) && !Directory.EnumerateFileSystemEntries(targetDir).Any())
                {
                    Directory.Delete(targetDir, false);
                    log?.Report($"[delete-dir] {relative}");
                }
            }
        }, ct);
    }
}
