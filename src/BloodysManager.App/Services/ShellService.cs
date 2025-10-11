using System.Diagnostics;
using System.Text;

namespace BloodysManager.App.Services;

public sealed class ShellService
{
    public async Task<(int exitCode, string stdout, string stderr)> RunAsync(
        string fileName, string arguments, string? workingDir = null, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDir ?? "",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var sbOut = new StringBuilder();
        var sbErr = new StringBuilder();

        p.OutputDataReceived += (_, e) => { if (e.Data is not null) sbOut.AppendLine(e.Data); };
        p.ErrorDataReceived  += (_, e) => { if (e.Data is not null) sbErr.AppendLine(e.Data); };

        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        await Task.Run(() =>
        {
            while (!p.HasExited)
            {
                ct.ThrowIfCancellationRequested();
                Thread.Sleep(25);
            }
        }, ct);

        return (p.ExitCode, sbOut.ToString(), sbErr.ToString());
    }
}
