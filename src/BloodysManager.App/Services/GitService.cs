using LibGit2Sharp;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BloodysManager.App.Services;

public sealed class GitService
{
    public async Task<bool> CleanCloneToAsync(
        string repoUrl,
        string targetDir,
        IProgress<string>? log,
        CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (Directory.Exists(targetDir))
                {
                    // Inhalt löschen, Ordner behalten
                    foreach (var file in Directory.EnumerateFiles(targetDir, "*", SearchOption.AllDirectories))
                        File.SetAttributes(file, FileAttributes.Normal);
                    foreach (var dir in Directory.EnumerateDirectories(targetDir))
                        Directory.Delete(dir, true);
                    foreach (var file in Directory.EnumerateFiles(targetDir))
                        File.Delete(file);
                }
                else
                {
                    Directory.CreateDirectory(targetDir);
                }

                // WICHTIG: Progress gehört in FetchOptions, nicht direkt in CloneOptions
                var opts = new CloneOptions
                {
                    FetchOptions = new FetchOptions
                    {
                        OnTransferProgress = progress =>
                        {
                            log?.Report($"[git] objects {progress.ReceivedObjects}/{progress.TotalObjects}, bytes {progress.ReceivedBytes}");
                            return !ct.IsCancellationRequested;
                        }
                    }
                };

                log?.Report($"[git] cloning {repoUrl} → {targetDir} ...");
                Repository.Clone(repoUrl, targetDir, opts);
                log?.Report("[git] clone completed ✓");
                return true;
            }
            catch (Exception ex)
            {
                log?.Report($"[git] clone failed ✗  {ex.Message}");
                return false;
            }
        }, ct);
    }
}
