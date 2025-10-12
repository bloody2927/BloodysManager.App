using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BloodysManager.App.Models;

public sealed class ServerProfile : INotifyPropertyChanged
{
    string _name = "Server 1";
    string _pathLive = @"B:\\Server\\Live\\azerothcore-wotlk";
    string _pathCopy = @"B:\\Server\\Live_Copy\\azerothcore-wotlk-copy";
    string _pathBackup = @"B:\\Server\\Backup";
    string _pathBackupZip = @"B:\\Server\\BackupZip";
    string _worldExe = "worldserver.exe";
    string _authExe = "authserver.exe";

    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    public string PathLive
    {
        get => _pathLive;
        set => Set(ref _pathLive, value);
    }

    public string PathCopy
    {
        get => _pathCopy;
        set => Set(ref _pathCopy, value);
    }

    public string PathBackup
    {
        get => _pathBackup;
        set => Set(ref _pathBackup, value);
    }

    public string PathBackupZip
    {
        get => _pathBackupZip;
        set => Set(ref _pathBackupZip, value);
    }

    public string WorldExe
    {
        get => _worldExe;
        set => Set(ref _worldExe, value);
    }

    public string AuthExe
    {
        get => _authExe;
        set => Set(ref _authExe, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}
