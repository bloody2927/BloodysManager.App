using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BloodysManager.App.Services;

public sealed class ProcessService
{
    public async Task<(int exitCode, Exception? error)> RunAsync(
        string exePath,
        string? args,
        string? workingDir,
        Action<string>? onOut,
        Action<string>? onErr,
        CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args ?? string.Empty,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDir) ? Path.GetDirectoryName(exePath)! : workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            proc.OutputDataReceived += (_, e) => { if (e.Data != null) onOut?.Invoke(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) onErr?.Invoke(e.Data); };
            proc.Exited += (_, __) => tcs.TrySetResult(proc.ExitCode);

            if (!proc.Start())
                return (-1, new InvalidOperationException("Process could not start."));

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            using var reg = ct.Register(() =>
            {
                try
                {
                    if (!proc.HasExited)
                        proc.Kill(entireProcessTree: true);
                }
                catch
                {
                }
            });

            var exit = await tcs.Task.ConfigureAwait(false);
            return (exit, null);
        }
        catch (Exception ex)
        {
            return (-1, ex);
        }
    }
}
