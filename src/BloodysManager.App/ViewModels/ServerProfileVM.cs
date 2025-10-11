using System.IO;
using BloodysManager.App.Services;

namespace BloodysManager.App.ViewModels;

public sealed class ServerProfileVM : ViewModelBase
{
    string _name = string.Empty;
    string? _livePath;
    string? _copyPath;
    string? _backupRoot;
    string? _backupZipRoot;
    string? _worldExePath;
    string? _authExePath;

    public ServerProfileVM()
    {
    }

    public ServerProfileVM(ServerProfileConfig cfg)
    {
        _name = cfg.Name;
        _livePath = cfg.LivePath;
        _copyPath = cfg.CopyPath;
        _backupRoot = cfg.BackupRoot;
        _backupZipRoot = cfg.BackupZipRoot;
        _worldExePath = cfg.WorldExePath;
        _authExePath = cfg.AuthExePath;
        OnAnyChanged();
    }

    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    public string? LivePath
    {
        get => _livePath;
        set
        {
            if (Set(ref _livePath, value))
                OnAnyChanged();
        }
    }

    public string? CopyPath
    {
        get => _copyPath;
        set
        {
            if (Set(ref _copyPath, value))
                OnAnyChanged();
        }
    }

    public string? BackupRoot
    {
        get => _backupRoot;
        set
        {
            if (Set(ref _backupRoot, value))
                OnAnyChanged();
        }
    }

    public string? BackupZipRoot
    {
        get => _backupZipRoot;
        set
        {
            if (Set(ref _backupZipRoot, value))
                OnAnyChanged();
        }
    }

    public string? WorldExePath
    {
        get => _worldExePath;
        set
        {
            if (Set(ref _worldExePath, value))
                OnAnyChanged();
        }
    }

    public string? AuthExePath
    {
        get => _authExePath;
        set
        {
            if (Set(ref _authExePath, value))
                OnAnyChanged();
        }
    }

    public string LiveRoot => string.IsNullOrWhiteSpace(LivePath)
        ? string.Empty
        : Path.GetDirectoryName(LivePath) ?? LivePath;

    public string CopyRoot => string.IsNullOrWhiteSpace(CopyPath)
        ? string.Empty
        : Path.GetDirectoryName(CopyPath) ?? CopyPath;

    public string ResolvedWorldExe => !string.IsNullOrWhiteSpace(WorldExePath)
        ? WorldExePath!
        : Path.Combine(LivePath ?? string.Empty, "bin", "worldserver.exe");

    public string ResolvedAuthExe => !string.IsNullOrWhiteSpace(AuthExePath)
        ? AuthExePath!
        : Path.Combine(LivePath ?? string.Empty, "bin", "authserver.exe");

    bool _existsLive;
    bool _existsCopy;
    bool _existsBackup;
    bool _existsBackupZip;

    public bool ExistsLive
    {
        get => _existsLive;
        private set => Set(ref _existsLive, value);
    }

    public bool ExistsCopy
    {
        get => _existsCopy;
        private set => Set(ref _existsCopy, value);
    }

    public bool ExistsBackup
    {
        get => _existsBackup;
        private set => Set(ref _existsBackup, value);
    }

    public bool ExistsBackupZip
    {
        get => _existsBackupZip;
        private set => Set(ref _existsBackupZip, value);
    }

    public void OnAnyChanged()
    {
        ExistsLive = !string.IsNullOrWhiteSpace(LivePath) && Directory.Exists(LivePath);
        ExistsCopy = !string.IsNullOrWhiteSpace(CopyPath) && Directory.Exists(CopyPath);
        ExistsBackup = !string.IsNullOrWhiteSpace(BackupRoot) && Directory.Exists(BackupRoot);
        ExistsBackupZip = !string.IsNullOrWhiteSpace(BackupZipRoot) && Directory.Exists(BackupZipRoot);
        Raise(nameof(ResolvedWorldExe));
        Raise(nameof(ResolvedAuthExe));
    }
}
