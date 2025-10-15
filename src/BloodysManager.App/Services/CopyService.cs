using System;
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

    public async Task MirrorLiveToCopyAsync(ServerProfile sp, IProgress<string>? log, CancellationToken ct)
    {
        await Task.Run(() =>
        {
            EnsureDir(sp.PathCopy);
            if (string.IsNullOrWhiteSpace(sp.PathLive) || !Directory.Exists(sp.PathLive))
            {
                log?.Report("✗ Live path not found.");
                return;
            }

            log?.Report($"[copy] Live → Copy  ({sp.PathLive} → {sp.PathCopy})");
            RobocopyMirror(sp.PathLive, sp.PathCopy, log, ct);
            log?.Report("[copy] completed ✓");
        }, ct);
    }

    public async Task BackupFromLiveAsync(ServerProfile sp, IProgress<string>? log, CancellationToken ct)
    {
        await Task.Run(() =>
        {
            EnsureDir(sp.PathBackup);
            if (string.IsNullOrWhiteSpace(sp.PathLive) || !Directory.Exists(sp.PathLive))
            {
                log?.Report("✗ Live path not found.");
                return;
            }

            log?.Report($"[backup] Live → Backup  ({sp.PathLive} → {sp.PathBackup})");
            RobocopyMirror(sp.PathLive, sp.PathBackup, log, ct);
            log?.Report("[backup] completed ✓");
        }, ct);
    }

    public async Task RotateBackupAsync(ServerProfile sp, int keep = 5, IProgress<string>? log = null, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            if (!Directory.Exists(sp.PathBackup))
            {
                log?.Report("ℹ backup folder does not exist — nothing to rotate.");
                return;
            }

            var entries = new DirectoryInfo(sp.PathBackup)
                .GetDirectories()
                .OrderByDescending(d => d.CreationTimeUtc)
                .ToList();

            for (int i = keep; i < entries.Count; i++)
            {
                if (ct.IsCancellationRequested) break;
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

    private static void RobocopyMirror(string source, string dest, IProgress<string>? log, CancellationToken ct)
    {
        // Robocopy spiegelt zuverlässig, auch große Bäume.
        // /MIR spiegelt, /R:1 /W:1 reduziert Wartezeiten
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "robocopy.exe",
            Arguments = $"\"{source}\" \"{dest}\" /MIR /R:1 /W:1 /NFL /NDL /NP",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var p = System.Diagnostics.Process.Start(psi)!;
        p.OutputDataReceived += (_, e) => { if (e.Data != null) log?.Report(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) log?.Report(e.Data); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        p.WaitForExit();
    }
}
