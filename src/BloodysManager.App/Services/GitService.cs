using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

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

    public async Task<string> CleanCloneAsync(CancellationToken ct)
    {
        var dst = _cfg.LivePath ?? throw new InvalidOperationException("Live path not configured.");
        if (Directory.Exists(dst)) Directory.Delete(dst, true);
        Directory.CreateDirectory(Path.GetDirectoryName(dst) ?? dst);

        if (!GitAvailable()) return await ZipFallbackAsync(ct);

        var args = $"clone --progress --depth 1 {_cfg.RepositoryUrl} \"{dst}\"";
        var (code, _, err) = await _shell.RunAsync("git", args, null, ct);
        if (code != 0) throw new Exception($"git clone failed: {err}");

        var (code2, stdout2, _) = await _shell.RunAsync("git", "-C \"" + dst + "\" rev-parse HEAD", null, ct);
        if (code2 != 0) throw new Exception("git rev-parse failed");

        var commit = stdout2.Trim();
        File.WriteAllText(Path.Combine(dst, "commit.txt"), commit);
        return commit;
    }

    public async Task<string> UpdateAsync(CancellationToken ct)
    {
        var dst = _cfg.LivePath ?? throw new InvalidOperationException("Live path not configured.");
        if (Directory.Exists(Path.Combine(dst, ".git")) && GitAvailable())
        {
            var (c1, _, e1) = await _shell.RunAsync("git", $"-C \"{dst}\" pull --ff-only", null, ct);
            if (c1 != 0) throw new Exception($"git pull failed: {e1}");

            var (c3, so3, _) = await _shell.RunAsync("git", $"-C \"{dst}\" rev-parse HEAD", null, ct);
            if (c3 != 0) throw new Exception("git rev-parse failed");

            var commit = so3.Trim();
            File.WriteAllText(Path.Combine(dst, "commit.txt"), commit);
            return commit;
        }
        return await CleanCloneAsync(ct);
    }

    async Task<string> ZipFallbackAsync(CancellationToken ct)
    {
        var baseUrl = _cfg.RepositoryUrl.TrimEnd('/');
        if (baseUrl.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            baseUrl = baseUrl[..^4];

        string[] branches = ["master", "main"]; // try master first for legacy repos
        foreach (var branch in branches)
        {
            try
            {
                var zipUrl = $"{baseUrl}/archive/refs/heads/{branch}.zip";
                var temp = Path.Combine(Path.GetTempPath(), "acore_" + Guid.NewGuid());
                Directory.CreateDirectory(temp);
                var zip = Path.Combine(temp, "repo.zip");

                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.ParseAdd("BloodysManager/1.0");
                var data = await http.GetByteArrayAsync(zipUrl, ct);
                await File.WriteAllBytesAsync(zip, data, ct);

                ZipFile.ExtractToDirectory(zip, temp);
                var extracted = Directory.EnumerateDirectories(temp)
                    .First();

                var livePath = _cfg.LivePath ?? throw new InvalidOperationException("Live path not configured.");
                if (Directory.Exists(livePath)) Directory.Delete(livePath, true);
                Directory.Move(extracted, livePath);

                var tag = $"ZIP-{branch}-{DateTime.Now:yyyyMMdd-HHmm}";
                File.WriteAllText(Path.Combine(livePath, "commit.txt"), tag);

                Directory.Delete(temp, true);
                return tag;
            }
            catch
            {
                // try next branch
            }
        }
        throw new Exception("Failed to download repository archive.");
    }
}
