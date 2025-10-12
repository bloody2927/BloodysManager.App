using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BloodysManager.App.Services;

public sealed class GitService
{
    private readonly Config _cfg;

    public GitService(Config cfg)
    {
        _cfg = cfg;
    }

    private static async Task<(int code, string stdout, string stderr)> RunGitAsync(string args, string? workDir, CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("git", args)
            {
                WorkingDirectory = workDir ?? Environment.CurrentDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        return (process.ExitCode, stdout, stderr);
    }

    private async Task<string> CloneFreshAsync(string repoUrl, string? branchOrRef, string destination, CancellationToken ct)
    {
        var temp = Path.Combine(Path.GetTempPath(), "bm_clone_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        try
        {
            var args = $"clone -c gc.auto=0 --depth 1 {Escape(repoUrl)} {Escape(temp)}";
            var (code, stdout, stderr) = await RunGitAsync(args, null, ct).ConfigureAwait(false);
            if (code != 0)
            {
                throw new InvalidOperationException($"git clone failed: {stderr}\n{stdout}");
            }

            if (!string.IsNullOrWhiteSpace(branchOrRef))
            {
                var checkoutArgs = $"-C {Escape(temp)} checkout {Escape(branchOrRef!)}";
                var (checkoutCode, checkoutStdout, checkoutStderr) = await RunGitAsync(checkoutArgs, null, ct).ConfigureAwait(false);
                if (checkoutCode != 0)
                {
                    throw new InvalidOperationException($"git checkout failed: {checkoutStderr}\n{checkoutStdout}");
                }
            }

            FileUtil.AtomicSwapDirectory(temp, destination);
            return await GetHeadCommitAsync(destination, ct).ConfigureAwait(false);
        }
        catch
        {
            FileUtil.ForceDeleteDirectory(temp);
            throw;
        }
    }

    private static async Task<string> GetHeadCommitAsync(string repoDir, CancellationToken ct)
    {
        var (code, stdout, stderr) = await RunGitAsync($"-C {Escape(repoDir)} rev-parse HEAD", null, ct).ConfigureAwait(false);
        if (code != 0)
        {
            throw new InvalidOperationException($"git rev-parse failed: {stderr}");
        }

        return stdout.Trim();
    }

    private static string Escape(string value)
        => "\"" + value.Replace("\"", "\\\"") + "\"";

    public Task<string> CleanCloneAsync(CancellationToken ct)
    {
        return CleanCloneAsync(ct, null, null);
    }

    public async Task<string> CleanCloneAsync(CancellationToken ct, string? destination, string? repositoryUrl)
    {
        var repo = string.IsNullOrWhiteSpace(repositoryUrl) ? _cfg.RepositoryUrl : repositoryUrl;
        if (string.IsNullOrWhiteSpace(repo))
        {
            throw new InvalidOperationException("Repository URL is not configured.");
        }

        var target = string.IsNullOrWhiteSpace(destination) ? _cfg.LivePath : destination;
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new InvalidOperationException("Destination path is not configured.");
        }

        PrepareTargetDirectory(target!);
        return await CloneFreshAsync(repo!, _cfg.RepositoryRef, target!, ct).ConfigureAwait(false);
    }

    public Task<string> UpdateAsync(CancellationToken ct)
    {
        return UpdateAsync(ct, null, null);
    }

    public async Task<string> UpdateAsync(CancellationToken ct, string? destination, string? repositoryUrl)
    {
        var repo = string.IsNullOrWhiteSpace(repositoryUrl) ? _cfg.RepositoryUrl : repositoryUrl;
        if (string.IsNullOrWhiteSpace(repo))
        {
            throw new InvalidOperationException("Repository URL is not configured.");
        }

        var target = string.IsNullOrWhiteSpace(destination) ? _cfg.LivePath : destination;
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new InvalidOperationException("Destination path is not configured.");
        }

        PrepareTargetDirectory(target!);
        return await CloneFreshAsync(repo!, _cfg.RepositoryRef, target!, ct).ConfigureAwait(false);
    }

    private static void PrepareTargetDirectory(string destination)
    {
        var parent = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(parent))
        {
            FileUtil.EnsureDirectory(parent, hardenedForCurrentUser: true);
        }
    }
}
