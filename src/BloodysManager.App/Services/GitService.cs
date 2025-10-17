using System;
using System.IO;
using System.Linq;
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
    public Task CloneAsync(string repoUrl, string targetDir, global::System.Action<string>? log)
        => CloneAsync(repoUrl, targetDir, log, CancellationToken.None);

    public Task CloneAsync(string repoUrl, string targetDir, global::System.Action<string>? log, CancellationToken ct)
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

            var fullTarget = Path.GetFullPath(targetDir);
            var parent = Path.GetDirectoryName(fullTarget);
            if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
            {
                Directory.CreateDirectory(parent);
            }

            if (Directory.Exists(fullTarget))
            {
                if (Repository.IsValid(fullTarget))
                {
                    log?.Report("[git] target already contains a repository – skipping clone");
                    return;
                }

                if (Directory.EnumerateFileSystemEntries(fullTarget).Any())
                    throw new IOException($"Target directory '{fullTarget}' already exists and is not empty.");

                Directory.Delete(fullTarget);
            }

            var fetchOptions = new FetchOptions();
            fetchOptions.OnTransferProgress = p =>
            {
                log?.Report(
                    $"[git] recv {p.ReceivedObjects}/{p.TotalObjects} | " +
                    $"idx {p.IndexedObjects} | bytes {p.ReceivedBytes}");
                return !ct.IsCancellationRequested;
            };

            fetchOptions.OnProgress = text =>
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    log?.Report($"[git] {text.Trim()}");
                }

                return !ct.IsCancellationRequested;
            };

            var options = new CloneOptions
            {
                RecurseSubmodules = true,
                IsBare = false,
                FetchOptions = fetchOptions
            };

            Repository.Clone(repoUrl, fullTarget, options);
            log?.Report("[git] clone done");
        }, ct);
    }

    public Task PullAsync(string workDir, global::System.Action<string>? log)
        => PullAsync(workDir, log, CancellationToken.None);

    public Task PullAsync(string workDir, global::System.Action<string>? log, CancellationToken ct)
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
            var fetchOptions = new FetchOptions();

            // Gemeinsames, versionssicheres Progress-Logging für LibGit2Sharp
            fetchOptions.OnTransferProgress = p =>
            {
                log?.Report(
                    $"[git] recv {p.ReceivedObjects}/{p.TotalObjects} | " +
                    $"idx {p.IndexedObjects} | bytes {p.ReceivedBytes}");
                return !ct.IsCancellationRequested;
            };

            fetchOptions.OnProgress = text =>
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    log?.Report($"[git] {text.Trim()}");
                }

                return !ct.IsCancellationRequested;
            };

            var options = new PullOptions
            {
                FetchOptions = fetchOptions
            };

            log?.Report("[git] pull …");
            var result = Commands.Pull(repo, signature, options);
            log?.Report($"[git] pull: {result.Status}");
        }, ct);
    }
}
