using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;

namespace BloodysManager.App.Services;

public sealed class GitService
{
    public async Task CloneAsync(string repoUrl, string targetDir, Action<string> log, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            log($"[Git] Clone → {targetDir}");
            if (Directory.Exists(targetDir) && Directory.EnumerateFileSystemEntries(targetDir).Any())
                throw new InvalidOperationException("Target directory is not empty.");

            var options = new CloneOptions
            {
                Checkout = true,
                OnTransferProgress = progress =>
                {
                    log($"[Git] receiving {progress.ReceivedObjects}/{progress.TotalObjects} objects, deltas {progress.ReceivedDeltas}");
                    return !ct.IsCancellationRequested;
                },
                OnCheckoutProgress = (path, completed, total) => log($"[Git] checkout {completed}/{total} : {path}")
            };

            Repository.Clone(repoUrl, targetDir, options);
            log("[Git] Clone completed.");
        }, ct).ConfigureAwait(false);
    }

    public async Task PullAsync(string repoDir, Action<string> log, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            using var repo = new Repository(repoDir);
            log("[Git] Fetch origin");
            var remote = repo.Network.Remotes["origin"];
            var fetchOptions = new FetchOptions
            {
                OnTransferProgress = progress =>
                {
                    log($"[Git] fetch {progress.ReceivedObjects}/{progress.TotalObjects}");
                    return !ct.IsCancellationRequested;
                }
            };

            Commands.Fetch(repo, remote.Name, remote.FetchRefSpecs.Select(spec => spec.Specification), fetchOptions, "[Git] fetch");

            var headName = repo.Head.FriendlyName;
            log($"[Git] Merge origin/{headName} → {headName}");
            var signature = repo.Config.BuildSignature(DateTimeOffset.Now);
            var mergeResult = repo.Merge(repo.Branches[$"origin/{headName}"], signature, new MergeOptions());
            log($"[Git] Merge result: {mergeResult.Status}");
        }, ct).ConfigureAwait(false);
    }
}
