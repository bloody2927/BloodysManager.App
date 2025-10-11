using System.Diagnostics;
using System.Security.Principal;

namespace BloodysManager.App.Services;

public static class AdminService
{
    public static bool IsElevated()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// Startet die App als Admin neu. Gibt TRUE zurück, wenn ein neuer Prozess
    /// gestartet wurde. Beendet den aktuellen Prozess **nur dann**.
    public static bool TryRelaunchAsAdmin()
    {
        try
        {
            var exe = Process.GetCurrentProcess().MainModule!.FileName!;
            var psi = new ProcessStartInfo(exe) { Verb = "runas", UseShellExecute = true };
            var p = Process.Start(psi);
            if (p == null) return false;      // nichts gestartet (abgebrochen)
            System.Windows.Application.Current.Shutdown(); // aktuellen Prozess sauber beenden
            return true;
        }
        catch
        {
            // UAC abgebrochen → nicht beenden
            return false;
        }
    }
}

/*


//using System.Diagnostics;
//using System.Security.Principal;

namespace BloodysManager.App.Services;

public static class AdminService
{
    public static bool IsElevated()
    {
        using var id = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(id);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void RelaunchAsAdmin()
    {
        var exe = Process.GetCurrentProcess().MainModule!.FileName!;
        var psi = new ProcessStartInfo(exe) { Verb = "runas", UseShellExecute = true };
        Process.Start(psi);
        Environment.Exit(0);
    }
}
*/