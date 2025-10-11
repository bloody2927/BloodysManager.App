using System.IO;
using BloodysManager.App.ViewModels;

namespace BloodysManager.App.Services;

public sealed class CopyService
{
    private readonly ShellService _shell; private readonly Config _cfg;
    public CopyService(ShellService s, Config c) { _shell = s; _cfg = c; }

    public async Task MirrorLiveToCopyAsync(ServerProfileVM profile, CancellationToken ct)
    {
        var livePath = profile.LivePath ?? throw new InvalidOperationException("Live path not configured.");
        var copyPath = profile.CopyPath ?? throw new InvalidOperationException("Copy path not configured.");

        if (!Directory.Exists(livePath))
            throw new DirectoryNotFoundException(livePath);

        var copyRoot = profile.CopyRoot;
        if (!string.IsNullOrWhiteSpace(copyRoot))
            Directory.CreateDirectory(copyRoot);

        var args = $"/MIR /COPY:DAT /R:1 /W:1 /MT:{_cfg.Threads} /NFL /NDL /NP /XD .git /XF .git \"{livePath}\" \"{copyPath}\"";
        var (code, _, err) = await _shell.RunAsync("robocopy.exe", args, null, ct);
        if (code >= 8) throw new Exception($"robocopy failed (code {code}): {err}");

        var commitTxt = Path.Combine(livePath, "commit.txt");
        if (File.Exists(commitTxt))
            File.Copy(commitTxt, Path.Combine(copyPath, "commit.txt"), true);
    }

    public Task DeleteLiveAsync(ServerProfileVM profile)
    {
        var liveRoot = profile.LiveRoot;
        if (!string.IsNullOrWhiteSpace(liveRoot) && Directory.Exists(liveRoot))
            Directory.Delete(liveRoot, true);
        return Task.CompletedTask;
    }

    public Task DeleteCopyAsync(ServerProfileVM profile)
    {
        var copyRoot = profile.CopyRoot;
        if (!string.IsNullOrWhiteSpace(copyRoot) && Directory.Exists(copyRoot))
            Directory.Delete(copyRoot, true);
        return Task.CompletedTask;
    }

    public Task CreateBaseFoldersAsync(ServerProfileVM profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.LiveRoot))
            Directory.CreateDirectory(profile.LiveRoot);
        if (!string.IsNullOrWhiteSpace(profile.CopyRoot))
            Directory.CreateDirectory(profile.CopyRoot);
        if (!string.IsNullOrWhiteSpace(profile.BackupRoot))
            Directory.CreateDirectory(profile.BackupRoot);
        return Task.CompletedTask;
    }
}
