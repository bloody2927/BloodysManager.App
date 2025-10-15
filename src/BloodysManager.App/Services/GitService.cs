using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;

namespace BloodysManager.App.Services;

/// <summary>
/// Git-Operationen (Clone/Pull) mit Fortschritt ins Log. Nutzt LibGit2Sharp.
/// Achtung: FetchOptions sind schreibgeschützt (read-only) – Callbacks müssen
/// über die vorhandene Instanz gesetzt werden (OnTransferProgress / OnProgress).
/// </summary>
public sealed class GitService
{
    public Task CloneAsync(string repoUrl, string targetDir, Action<string>? log)
        => CloneAsync(repoUrl, targetDir, log, CancellationToken.None);

    public Task CloneAsync(string repoUrl, string targetDir, Action<string>? log, CancellationToken ct)
        => CloneAsync(repoUrl, targetDir, log is null ? null : new Progress<string>(log), ct);

    public Task CloneAsync(string repoUrl, string targetDir, IProgress<string>? log)
        => CloneAsync(repoUrl, targetDir, log, CancellationToken.None);

    /// <summary>
    /// Klont ein Repository in <paramref name="targetDir"/>.
    /// </summary>
    public Task CloneAsync(
        string repoUrl,
        string targetDir,
        IProgress<string>? log,
        CancellationToken ct)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(repoUrl))
                throw new ArgumentException("Repository URL is empty.", nameof(repoUrl));
            if (string.IsNullOrWhiteSpace(targetDir))
                throw new ArgumentException("Target directory is empty.", nameof(targetDir));

            log?.Report($"[git] clone → {targetDir}");

            if (Directory.Exists(targetDir))
            {
                if (Repository.IsValid(targetDir))
                {
                    log?.Report("[git] target already contains a repository – skipping clone");
                    return;
                }
            }
            else
            {
                Directory.CreateDirectory(targetDir);
            }

            var options = new CloneOptions
            {
                RecurseSubmodules = true,
                IsBare = false
            };

            options.FetchOptions.OnTransferProgress = stats =>
            {
                // 'ReceivedDeltas' existiert in dieser LibGit2Sharp-Version nicht.
                // Stattdessen 'IndexedObjects' (fortschreitende Indexierung) loggen.
                log?.Report($"[git] objects {stats.ReceivedObjects}/{stats.TotalObjects}  indexed {stats.IndexedObjects}");
                return !ct.IsCancellationRequested;
            };

            options.FetchOptions.OnProgress = text =>
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    log?.Report($"[git] {text.Trim()}");
                }

                return !ct.IsCancellationRequested;
            };

            Repository.Clone(repoUrl, targetDir, options);
            log?.Report("[git] clone done");
        }, ct);
    }

    public Task PullAsync(string workDir, Action<string>? log)
        => PullAsync(workDir, log, CancellationToken.None);

    public Task PullAsync(string workDir, Action<string>? log, CancellationToken ct)
        => PullAsync(workDir, log is null ? null : new Progress<string>(log), ct);

    public Task PullAsync(string workDir, IProgress<string>? log)
        => PullAsync(workDir, log, CancellationToken.None);

    /// <summary>
    /// Führt ein Pull im bestehenden Repo in <paramref name="workDir"/> aus.
    /// </summary>
    public Task PullAsync(
        string workDir,
        IProgress<string>? log,
        CancellationToken ct)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            if (!Repository.IsValid(workDir))
                throw new InvalidOperationException($"No valid git repository at '{workDir}'.");

            using var repo = new Repository(workDir);
            var signature = new Signature("BloodysManager", "noreply@local", DateTimeOffset.Now);
            var options = new PullOptions();

            options.FetchOptions.OnTransferProgress = stats =>
            {
                log?.Report($"[git] fetch {stats.ReceivedObjects}/{stats.TotalObjects}");
                return !ct.IsCancellationRequested;
            };

            options.FetchOptions.OnProgress = text =>
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    log?.Report($"[git] {text.Trim()}");
                }

                return !ct.IsCancellationRequested;
            };

            log?.Report("[git] pull …");
            var result = Commands.Pull(repo, signature, options);
            log?.Report($"[git] pull: {result.Status}");
        }, ct);
    }
}
