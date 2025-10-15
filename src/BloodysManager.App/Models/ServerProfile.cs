using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BloodysManager.App.Models;

/// <summary>
/// Repräsentiert einen konfigurierten Server mit allen relevanten Pfaden.
/// </summary>
public class ServerProfile : INotifyPropertyChanged
{
    private string _name = "Server 1";
    private string _pathLive = "";
    private string _pathCopy = "";
    private string _pathBackup = "";
    private string _pathBackupZip = "";

    public string Name
    {
        get => _name;
        set { if (_name != value) { _name = value; OnPropertyChanged(); } }
    }

    /// <summary>Pfad zum Live-Server.</summary>
    public string PathLive
    {
        get => _pathLive;
        set { if (_pathLive != value) { _pathLive = value; OnPropertyChanged(); } }
    }

    /// <summary>Pfad zur Live-Kopie.</summary>
    public string PathCopy
    {
        get => _pathCopy;
        set { if (_pathCopy != value) { _pathCopy = value; OnPropertyChanged(); } }
    }

    /// <summary>Pfad zum Backup-Ordner.</summary>
    public string PathBackup
    {
        get => _pathBackup;
        set { if (_pathBackup != value) { _pathBackup = value; OnPropertyChanged(); } }
    }

    /// <summary>Pfad zum Backup-Zip-Ordner.</summary>
    public string PathBackupZip
    {
        get => _pathBackupZip;
        set { if (_pathBackupZip != value) { _pathBackupZip = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

