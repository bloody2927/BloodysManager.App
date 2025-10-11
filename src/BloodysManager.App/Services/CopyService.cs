using System.IO;

namespace BloodysManager.App.Services;

public sealed class CopyService
{
    private readonly ShellService _shell; private readonly Config _cfg;
    public CopyService(ShellService s, Config c) { _shell = s; _cfg = c; }

    public async Task MirrorLiveToCopyAsync(CancellationToken ct)
    {
        if (!Directory.Exists(_cfg.LivePath))
            throw new DirectoryNotFoundException(_cfg.LivePath);

        Directory.CreateDirectory(_cfg.CopyRoot);
        var args = $"/MIR /COPY:DAT /R:1 /W:1 /MT:{_cfg.Threads} /NFL /NDL /NP /XD .git /XF .git \"{_cfg.LivePath}\" \"{_cfg.CopyPath}\"";
        var (code, _, err) = await _shell.RunAsync("robocopy.exe", args, null, ct);
        if (code >= 8) throw new Exception($"robocopy failed (code {code}): {err}");

        var commitTxt = Path.Combine(_cfg.LivePath, "commit.txt");
        if (File.Exists(commitTxt))
            File.Copy(commitTxt, Path.Combine(_cfg.CopyPath, "commit.txt"), true);
    }

    public Task DeleteLiveAsync() { if (Directory.Exists(_cfg.LiveRoot)) Directory.Delete(_cfg.LiveRoot, true); return Task.CompletedTask; }
    public Task DeleteCopyAsync() { if (Directory.Exists(_cfg.CopyRoot)) Directory.Delete(_cfg.CopyRoot, true); return Task.CompletedTask; }

    public Task CreateBaseFoldersAsync()
    {
        Directory.CreateDirectory(_cfg.LiveRoot);
        Directory.CreateDirectory(_cfg.CopyRoot);
        Directory.CreateDirectory(_cfg.BackupRoot);
        return Task.CompletedTask;
    }
}
