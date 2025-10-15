using System;
using System.Diagnostics;
using System.IO;

namespace BloodysManager.App.Services;

public sealed class ProcessService
{
    public Process? StartExe(string exePath, Action<string>? log = null)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            throw new FileNotFoundException("Executable not found", exePath);

        var psi = new ProcessStartInfo(exePath)
        {
            WorkingDirectory = Path.GetDirectoryName(exePath)!,
            UseShellExecute = false,
            CreateNoWindow = false
        };

        var process = Process.Start(psi);
        log?.Invoke($"[proc] started: {exePath} (pid {process?.Id})");
        return process;
    }

    public bool StopByPath(string exePath, Action<string>? log = null)
    {
        var name = Path.GetFileNameWithoutExtension(exePath);
        var any = false;

        foreach (var process in Process.GetProcessesByName(name))
        {
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
                any = true;
            }
            catch
            {
                // ignore
            }
        }

        if (any)
            log?.Invoke($"[proc] stopped: {name}");

        return any;
    }

    public Process? Restart(string exePath, Action<string>? log = null)
    {
        StopByPath(exePath, log);
        return StartExe(exePath, log);
    }

    public static bool IsRunning(string exePath)
    {
        var name = Path.GetFileNameWithoutExtension(exePath);
        return !string.IsNullOrWhiteSpace(name) && Process.GetProcessesByName(name).Length > 0;
    }
}
