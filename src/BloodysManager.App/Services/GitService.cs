using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;

namespace BloodysManager.App.Services;

public sealed class GitService
{
    private static void EnsureDirectory(string targetDir)
    {
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
            return;
        }

        foreach (var file in Directory.EnumerateFiles(targetDir, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        foreach (var directory in Directory.EnumerateDirectories(targetDir))
        {
            Directory.Delete(directory, recursive: true);
        }

        foreach (var file in Directory.EnumerateFiles(targetDir))
        {
            File.Delete(file);
        }
    }

    public Task CloneAsync(string repoUrl, string targetDir, Action<string>? log)
        => CloneAsync(repoUrl, targetDir, log, CancellationToken.None);

    public Task CloneAsync(string repoUrl, string targetDir, Action<string>? log, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(repoUrl))
                throw new ArgumentException("Repository URL is empty.", nameof(repoUrl));
            if (string.IsNullOrWhiteSpace(targetDir))
                throw new ArgumentException("Target directory is empty.", nameof(targetDir));

            log?.Invoke($"[git] clone {repoUrl} → {targetDir}");
            EnsureDirectory(targetDir);

            var options = new CloneOptions
            {
                FetchOptions = new FetchOptions
                {
                    TransferProgress = progress =>
                    {
                        log?.Invoke($"[git] objects {progress.ReceivedObjects}/{progress.TotalObjects} deltas {progress.ReceivedDeltas}");
                        return !cancellationToken.IsCancellationRequested;
                    }
                }
            };

            Repository.Clone(repoUrl, targetDir, options);
            log?.Invoke("[git] clone completed");
        }, cancellationToken);
    }

    public Task PullAsync(string workDir, Action<string>? log)
        => PullAsync(workDir, log, CancellationToken.None);

    public Task PullAsync(string workDir, Action<string>? log, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            if (!Repository.IsValid(workDir))
                throw new InvalidOperationException($"Not a git repository: {workDir}");

            using var repo = new Repository(workDir);
            var signature = new Signature("BloodysManager", "noreply@local", DateTimeOffset.Now);

            var options = new PullOptions
            {
                FetchOptions = new FetchOptions
                {
                    TransferProgress = progress =>
                    {
                        log?.Invoke($"[git] fetch {progress.ReceivedObjects}/{progress.TotalObjects}");
                        return !cancellationToken.IsCancellationRequested;
                    }
                }
            };

            log?.Invoke("[git] pull …");
            var result = Commands.Pull(repo, signature, options);
            log?.Invoke($"[git] pull result: {result.Status}");
        }, cancellationToken);
    }
}
