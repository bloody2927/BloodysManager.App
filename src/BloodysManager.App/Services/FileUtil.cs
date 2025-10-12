using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace BloodysManager.App.Services;

public static class FileUtil
{
    public static void ForceDeleteDirectory(string path, int maxRetries = 12, int delayMs = 250)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;

        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            try { File.SetAttributes(file, FileAttributes.Normal); }
            catch { }
        }

        foreach (var dir in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories))
        {
            try { new DirectoryInfo(dir).Attributes = FileAttributes.Normal; }
            catch { }
        }

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                Directory.Delete(path, true);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(delayMs);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(delayMs);
            }
        }

        Directory.Delete(path, true);
    }

    public static void AtomicSwapDirectory(string srcTemp, string dst)
    {
        if (!Directory.Exists(srcTemp)) throw new DirectoryNotFoundException(srcTemp);
        if (Directory.Exists(dst)) ForceDeleteDirectory(dst);
        Directory.Move(srcTemp, dst);
    }

    public static void EnsureDirectory(string path, bool hardenedForCurrentUser = false)
    {
        if (Directory.Exists(path)) return;
        Directory.CreateDirectory(path);

        if (!hardenedForCurrentUser) return;

        try
        {
            var dirInfo = new DirectoryInfo(path);
            var sec = dirInfo.GetAccessControl();

            var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            sec.PurgeAccessRules(everyone);

            var user = WindowsIdentity.GetCurrent().User!;
            sec.SetOwner(user);
            sec.AddAccessRule(new FileSystemAccessRule(
                user,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            dirInfo.SetAccessControl(sec);
        }
        catch
        {
        }
    }

    public static void MirrorTree(string src, string dst, Func<string, bool>? exclude = null, CancellationToken ct = default)
    {
        if (!Directory.Exists(src)) throw new DirectoryNotFoundException(src);
        ForceDeleteDirectory(dst);
        Directory.CreateDirectory(dst);

        foreach (var dir in Directory.EnumerateDirectories(src, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            if (exclude != null && exclude(dir)) continue;
            var rel = Path.GetRelativePath(src, dir);
            var target = Path.Combine(dst, rel);
            Directory.CreateDirectory(target);
        }

        foreach (var file in Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            if (exclude != null && exclude(file)) continue;
            var rel = Path.GetRelativePath(src, file);
            var target = Path.Combine(dst, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
