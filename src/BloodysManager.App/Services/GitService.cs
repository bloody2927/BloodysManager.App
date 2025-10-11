using System.IO;
using System.IO.Compression;
using System.Net.Http;
using BloodysManager.App.ViewModels;

namespace BloodysManager.App.Services;

public sealed class GitService
{
    private readonly ShellService _shell;
    private readonly Config _cfg;

    public GitService(ShellService shell, Config cfg) { _shell = shell; _cfg = cfg; }

    static bool GitAvailable() =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
        .Split(Path.PathSeparator)
        .Select(p => Path.Combine(p, "git.exe"))
        .Any(File.Exists);

    public async Task<string> CleanCloneAsync(ServerProfileVM profile, CancellationToken ct)
    {
        var dst = profile.LivePath ?? throw new InvalidOperationException("Live path not configured.");
        if (Directory.Exists(dst)) Directory.Delete(dst, true);
        var liveRoot = profile.LiveRoot;
        if (!string.IsNullOrWhiteSpace(liveRoot))
            Directory.CreateDirectory(liveRoot);

        if (!GitAvailable()) return await ZipFallbackAsync(profile, ct);

        var args = $"clone --progress --depth 1 --branch {_cfg.Branch} {_cfg.RepoUrl} \"{dst}\"";
        var (code, _, err) = await _shell.RunAsync("git", args, null, ct);
        if (code != 0) throw new Exception($"git clone failed: {err}");

        var (code2, stdout2, _) = await _shell.RunAsync("git", "-C \"" + dst + "\" rev-parse HEAD", null, ct);
        if (code2 != 0) throw new Exception("git rev-parse failed");

        var commit = stdout2.Trim();
        File.WriteAllText(Path.Combine(dst, "commit.txt"), commit);
        return commit;
    }

    public async Task<string> UpdateAsync(ServerProfileVM profile, CancellationToken ct)
    {
        var dst = profile.LivePath ?? throw new InvalidOperationException("Live path not configured.");
        if (Directory.Exists(Path.Combine(dst, ".git")) && GitAvailable())
        {
            var (c1, _, e1) = await _shell.RunAsync("git", $"-C \"{dst}\" fetch --all --progress", null, ct);
            if (c1 != 0) throw new Exception($"git fetch failed: {e1}");

            var (c2, _, e2) = await _shell.RunAsync("git", $"-C \"{dst}\" reset --hard origin/{_cfg.Branch}", null, ct);
            if (c2 != 0) throw new Exception($"git reset failed: {e2}");

            var (c3, so3, _) = await _shell.RunAsync("git", $"-C \"{dst}\" rev-parse HEAD", null, ct);
            if (c3 != 0) throw new Exception("git rev-parse failed");

            var commit = so3.Trim();
            File.WriteAllText(Path.Combine(dst, "commit.txt"), commit);
            return commit;
        }
        return await CleanCloneAsync(profile, ct);
    }

    private async Task<string> ZipFallbackAsync(ServerProfileVM profile, CancellationToken ct)
    {
        var zipUrl = $"{_cfg.RepoUrl.TrimEnd('/')}/archive/refs/heads/{_cfg.Branch}.zip";
        var temp = Path.Combine(Path.GetTempPath(), "acore_" + Guid.NewGuid());
        Directory.CreateDirectory(temp);
        var zip = Path.Combine(temp, "repo.zip");

        using var http = new HttpClient();
        var data = await http.GetByteArrayAsync(zipUrl, ct);
        await File.WriteAllBytesAsync(zip, data, ct);

        ZipFile.ExtractToDirectory(zip, temp);
        var extracted = Directory.EnumerateDirectories(temp)
            .First(d => Path.GetFileName(d).StartsWith("azerothcore-wotlk", StringComparison.OrdinalIgnoreCase));

        var livePath = profile.LivePath ?? throw new InvalidOperationException("Live path not configured.");
        if (Directory.Exists(livePath)) Directory.Delete(livePath, true);
        Directory.Move(extracted, livePath);

        var tag = $"ZIP-{_cfg.Branch}-{DateTime.Now:yyyyMMdd-HHmm}";
        File.WriteAllText(Path.Combine(livePath, "commit.txt"), tag);

        Directory.Delete(temp, true);
        return tag;
    }
}
