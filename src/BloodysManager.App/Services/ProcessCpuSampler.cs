using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace BloodysManager.App.Services;

/// <summary>Einfacher CPU%-Sampler: misst TotalProcessorTime-Delta / Echtzeit / CPU-Kerne.</summary>
public sealed class ProcessCpuSampler
{
    readonly int _cores = Environment.ProcessorCount;
    public async Task<double> SampleCpuPercentAsync(int pid, int milliseconds = 1000, CancellationToken ct = default)
    {
        var p = Process.GetProcessById(pid);
        var t0 = p.TotalProcessorTime;
        var sw = Stopwatch.StartNew();
        await Task.Delay(milliseconds, ct);
        p.Refresh();
        var t1 = p.TotalProcessorTime;
        var cpu = (t1 - t0).TotalMilliseconds / sw.Elapsed.TotalMilliseconds * 100.0 / _cores;
        return Math.Max(0, cpu);
    }
}
