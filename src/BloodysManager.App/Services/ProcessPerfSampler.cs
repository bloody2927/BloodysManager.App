using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BloodysManager.App.Services;

/// <summary>
/// Samples CPU and RAM usage for a single process.
/// </summary>
public sealed class ProcessPerfSampler : IDisposable
{
    private readonly string _exePath;
    private readonly TimeSpan _interval;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public double CpuPercent { get; private set; }
    public double RamMb { get; private set; }

    public ProcessPerfSampler(string exePath, TimeSpan? interval = null)
    {
        _exePath = exePath;
        _interval = interval ?? TimeSpan.FromSeconds(5);
    }

    public void Start()
    {
        if (_loop is not null)
            return;

        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
        _loop = null;
    }

    private async Task RunAsync(CancellationToken token)
    {
        var name = Path.GetFileNameWithoutExtension(_exePath);
        Process? process = null;
        TimeSpan lastTotal = TimeSpan.Zero;
        DateTime lastStamp = DateTime.UtcNow;

        while (!token.IsCancellationRequested)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    CpuPercent = 0;
                    RamMb = 0;
                }
                else
                {
                    process ??= Process.GetProcessesByName(name).FirstOrDefault();
                    if (process is { HasExited: false })
                    {
                        var now = DateTime.UtcNow;
                        var total = process.TotalProcessorTime;
                        var deltaSeconds = (now - lastStamp).TotalSeconds;
                        var cpuSeconds = (total - lastTotal).TotalSeconds;

                        lastStamp = now;
                        lastTotal = total;

                        CpuPercent = deltaSeconds > 0
                            ? cpuSeconds / deltaSeconds * 100.0 / Environment.ProcessorCount
                            : 0.0;

                        RamMb = process.WorkingSet64 / (1024.0 * 1024.0);
                    }
                    else
                    {
                        CpuPercent = 0;
                        RamMb = 0;
                        process = null;
                    }
                }
            }
            catch
            {
                CpuPercent = 0;
                RamMb = 0;
                process = null;
            }

            try
            {
                await Task.Delay(_interval, token).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}
