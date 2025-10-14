using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace BloodysManager.App.Models;

public class ServerProfile : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private string _name = "Server 1";
    private string _livePath = string.Empty;
    private string _copyPath = string.Empty;
    private string _backupPath = string.Empty;
    private string _backupZipPath = string.Empty;

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            Raise();
        }
    }

    public string LivePath
    {
        get => _livePath;
        set
        {
            if (_livePath == value) return;
            _livePath = value;
            Raise();
            Raise(nameof(ExistsLive));
        }
    }

    public string CopyPath
    {
        get => _copyPath;
        set
        {
            if (_copyPath == value) return;
            _copyPath = value;
            Raise();
            Raise(nameof(ExistsCopy));
        }
    }

    public string BackupPath
    {
        get => _backupPath;
        set
        {
            if (_backupPath == value) return;
            _backupPath = value;
            Raise();
            Raise(nameof(ExistsBackup));
        }
    }

    public string BackupZipPath
    {
        get => _backupZipPath;
        set
        {
            if (_backupZipPath == value) return;
            _backupZipPath = value;
            Raise();
            Raise(nameof(ExistsBackupZip));
        }
    }

    public bool ExistsLive => !string.IsNullOrWhiteSpace(LivePath) && Directory.Exists(LivePath);
    public bool ExistsCopy => !string.IsNullOrWhiteSpace(CopyPath) && Directory.Exists(CopyPath);
    public bool ExistsBackup => !string.IsNullOrWhiteSpace(BackupPath) && Directory.Exists(BackupPath);
    public bool ExistsBackupZip => !string.IsNullOrWhiteSpace(BackupZipPath) && Directory.Exists(BackupZipPath);
}
