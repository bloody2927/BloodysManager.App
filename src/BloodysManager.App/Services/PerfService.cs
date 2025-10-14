using System;
using System.Diagnostics;

namespace BloodysManager.App.Services;

public sealed class PerfService : IDisposable
{
    private readonly PerformanceCounter _cpu = new("Processor", "% Processor Time", "_Total");
    private readonly PerformanceCounter _ram = new("Memory", "% Committed Bytes In Use");

    public (float cpu, float ram) Sample()
    {
        var cpu = _cpu.NextValue();
        var ram = _ram.NextValue();
        return (cpu, ram);
    }

    public void Dispose()
    {
        _cpu.Dispose();
        _ram.Dispose();
    }
}
