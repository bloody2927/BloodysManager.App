using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using BloodysManager.App.Models;
using BloodysManager.App.Services;

namespace BloodysManager.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    bool Set<T>(ref T field, T value, [CallerMemberName] string? n = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new(n));
        return true;
    }

    readonly Config _cfg;
    readonly ShellService _shell = new();
    readonly GitService _git;
    readonly CopyService _copy;
    readonly BackupService _backup;

    string _repositoryUrl = string.Empty;
    string _downloadPath = string.Empty;
    bool _busy;

    public Localizer L { get; }

    readonly ObservableCollection<ServerProfile> _profiles = new();
    public ObservableCollection<ServerProfile> Profiles => _profiles;

    ServerProfile? _selectedProfile;
    public ServerProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (Set(ref _selectedProfile, value) && value is not null)
            {
                SyncConfigFromProfile(value);
                RefreshPathStates();
            }
            else if (value is null)
            {
                RefreshPathStates();
            }
        }
    }

    void OnProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is ServerProfile profile && profile == SelectedProfile)
        {
            SyncConfigFromProfile(profile);
            RefreshPathStates();
        }
    }

    public string RepositoryUrl
    {
        get => _repositoryUrl;
        set => Set(ref _repositoryUrl, value);
    }

    public string DownloadPath
    {
        get => _downloadPath;
        set => Set(ref _downloadPath, value);
    }

    string _log = string.Empty;
    public string Log
    {
        get => _log;
        set => Set(ref _log, value);
    }

    public bool IsBusy
    {
        get => _busy;
        set => Set(ref _busy, value);
    }

    public ICommand CmdSaveConfig { get; }
    public ICommand CmdBrowseDownloadPath { get; }
    public ICommand CmdDownload { get; }

    bool _existsLive, _existsCopy, _existsBackup, _existsBackupZip;
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

    public string AppTitle => "Bloody's Manager";

    public MainViewModel(Config cfg, GitService git, CopyService copy, BackupService backup)
    {
        _cfg = cfg;
        L = new Localizer(cfg);
        _git = git;
        _copy = copy;
        _backup = backup;

        RepositoryUrl = _cfg.RepositoryUrl ?? string.Empty;
        DownloadPath = _cfg.DownloadPath ?? string.Empty;

        CmdSaveConfig = new Relay(_ => SaveConfig(), () => !IsBusy);
        CmdBrowseDownloadPath = new Relay(_ => BrowseDownloadPath(), () => !IsBusy);
        CmdDownload = new Relay(async _ => await DownloadAsync(),
            () => !IsBusy
                  && !string.IsNullOrWhiteSpace(RepositoryUrl)
                  && !string.IsNullOrWhiteSpace(DownloadPath));

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(IsBusy) or nameof(RepositoryUrl) or nameof(DownloadPath))
            {
                (CmdSaveConfig as Relay)?.RiseCanExecuteChanged();
                (CmdBrowseDownloadPath as Relay)?.RiseCanExecuteChanged();
                (CmdDownload as Relay)?.RiseCanExecuteChanged();
            }
        };

        var initialProfile = new ServerProfile
        {
            Name = "Server 1",
            PathLive = _cfg.LivePath,
            PathCopy = _cfg.CopyPath,
            PathBackup = _cfg.BackupRoot,
            PathBackupZip = string.IsNullOrWhiteSpace(_cfg.BackupZip)
                ? Path.Combine(Path.GetDirectoryName(_cfg.BackupRoot) ?? _cfg.BackupRoot, "BackupZip")
                : _cfg.BackupZip,
        };
        AttachProfile(initialProfile);
        _profiles.Add(initialProfile);
        SelectedProfile = initialProfile;
    }

    void AttachProfile(ServerProfile profile)
    {
        profile.PropertyChanged += OnProfilePropertyChanged;
    }

    void DetachProfile(ServerProfile profile)
    {
        profile.PropertyChanged -= OnProfilePropertyChanged;
    }

    void SyncConfigFromProfile(ServerProfile profile)
    {
        _cfg.LivePath = profile.PathLive;
        _cfg.CopyPath = profile.PathCopy;
        _cfg.BackupRoot = profile.PathBackup;
        _cfg.BackupZip = profile.PathBackupZip;
    }

    void RefreshPathStates()
    {
        var p = SelectedProfile;
        if (p is null)
        {
            ExistsLive = ExistsCopy = ExistsBackup = ExistsBackupZip = false;
            return;
        }

        ExistsLive = Directory.Exists(p.PathLive);
        ExistsCopy = Directory.Exists(p.PathCopy);
        ExistsBackup = Directory.Exists(p.PathBackup);
        ExistsBackupZip = Directory.Exists(p.PathBackupZip);
    }

    void SaveConfig()
    {
        _cfg.RepositoryUrl = RepositoryUrl?.Trim() ?? string.Empty;
        _cfg.DownloadPath = DownloadPath?.Trim() ?? string.Empty;
        _cfg.Save();
        Ok("Configuration saved.");
    }

    void BrowseDownloadPath()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Choose download target folder",
            ShowNewFolderButton = true
        };
        if (dlg.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
        {
            DownloadPath = dlg.SelectedPath;
            _cfg.DownloadPath = DownloadPath;
            _cfg.Save();
            Ok($"Download path set: {DownloadPath}");
        }
    }

    async Task DownloadAsync()
    {
        await Guard(async ct =>
        {
            _cfg.RepositoryUrl = RepositoryUrl?.Trim() ?? string.Empty;
            _cfg.DownloadPath = DownloadPath?.Trim() ?? string.Empty;

            Ok($"Download started from: '{_cfg.RepositoryUrl}'");
            Ok($"Target: '{_cfg.DownloadPath}'");
            var head = await _git.CleanCloneToDownloadAsync(ct);
            Ok($"Download completed. HEAD: {head}");
        });
    }

    void Ok(string s) => Log += $"[{DateTime.Now:HH:mm:ss}] ✓ {s}\n";
    void Err(string s) => Log += $"[{DateTime.Now:HH:mm:ss}] ✗ {s}\n";

    async Task Guard(Func<CancellationToken, Task> op)
    {
        try
        {
            IsBusy = true;
            await op(CancellationToken.None);
        }
        catch (Exception ex)
        {
            Err(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    ServerProfile RequireProfile()
    {
        return SelectedProfile ?? throw new InvalidOperationException("No server profile selected.");
    }

    void BrowseAssign(ServerProfile profile, Action<string> setter, string title)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = title,
            ShowNewFolderButton = true
        };
        if (dlg.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
        {
            setter(dlg.SelectedPath);
            SyncConfigFromProfile(profile);
            _cfg.Save();
            RefreshPathStates();
            Ok($"Updated path for {profile.Name}.");
        }
    }

    public ICommand CmdExit => new Relay(_ => System.Windows.Application.Current.Shutdown());

    public ICommand CmdOpenSettings => new Relay(_ =>
        System.Windows.MessageBox.Show("Settings placeholder – edit appsettings.json.", "Settings"));

    public ICommand CmdOpenCredits => new Relay(_ =>
        System.Windows.MessageBox.Show("Bloody's Manager – MIT License © 2025", "Credits"));

    public ICommand CmdAddProfile => new Relay(_ =>
    {
        var idx = _profiles.Count + 1;
        var profile = new ServerProfile
        {
            Name = $"Server {idx}",
            PathLive = Path.Combine(@"B:\\Server", $"Live_{idx}", "azerothcore-wotlk"),
            PathCopy = Path.Combine(@"B:\\Server", $"Live_Copy_{idx}", "azerothcore-wotlk-copy"),
            PathBackup = Path.Combine(@"B:\\Server", $"Backup_{idx}"),
            PathBackupZip = Path.Combine(@"B:\\Server", $"BackupZip_{idx}"),
        };
        AttachProfile(profile);
        _profiles.Add(profile);
        SelectedProfile = profile;
        Ok($"Added profile {profile.Name}.");
    });

    public ICommand CmdRemoveProfile => new Relay(obj =>
    {
        if (obj is not ServerProfile profile) return;
        if (!_profiles.Contains(profile)) return;

        DetachProfile(profile);
        _profiles.Remove(profile);
        if (SelectedProfile == profile)
            SelectedProfile = _profiles.FirstOrDefault();

        RefreshPathStates();
        Ok($"Removed profile {profile.Name}.");
    });

    public ICommand CmdSetPathLive => new Relay(_ =>
    {
        var profile = RequireProfile();
        BrowseAssign(profile, v => profile.PathLive = v, "Select Live folder");
    });

    public ICommand CmdSetPathCopy => new Relay(_ =>
    {
        var profile = RequireProfile();
        BrowseAssign(profile, v => profile.PathCopy = v, "Select Copy folder");
    });

    public ICommand CmdSetPathBackup => new Relay(_ =>
    {
        var profile = RequireProfile();
        BrowseAssign(profile, v => profile.PathBackup = v, "Select Backup folder");
    });

    public ICommand CmdSetPathBackupZip => new Relay(_ =>
    {
        var profile = RequireProfile();
        BrowseAssign(profile, v => profile.PathBackupZip = v, "Select Backup Zip folder");
    });

    public ICommand CmdCreateFolders => new Relay(async _ =>
    {
        var profile = RequireProfile();
        using var dlg = new FolderBrowserDialog
        {
            Description = "Choose base folder for server structure",
            ShowNewFolderButton = true
        };
        if (dlg.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(dlg.SelectedPath))
            return;

        await _copy.CreateBaseFoldersAsync(dlg.SelectedPath, profile);
        SyncConfigFromProfile(profile);
        _cfg.Save();
        RefreshPathStates();
        Ok($"Folders created for {profile.Name}.");
    });

    public ICommand CmdCleanDownload => new Relay(async _ =>
    {
        await Guard(async ct =>
        {
            var profile = RequireProfile();
            SyncConfigFromProfile(profile);
            var commit = await _git.CleanCloneAsync(ct, profile.PathLive, RepositoryUrl);
            RefreshPathStates();
            Ok($"Download OK ({profile.Name}: {commit})");
        });
    });

    public ICommand CmdUpdate => new Relay(async _ =>
    {
        await Guard(async ct =>
        {
            var profile = RequireProfile();
            SyncConfigFromProfile(profile);
            var commit = await _git.UpdateAsync(ct, profile.PathLive, RepositoryUrl);
            RefreshPathStates();
            Ok($"Update OK ({profile.Name}: {commit})");
        });
    });

    public ICommand CmdCopy => new Relay(async _ =>
    {
        await Guard(async ct =>
        {
            var profile = RequireProfile();
            await _copy.MirrorLiveToCopyAsync(ct, profile.PathLive, profile.PathCopy);
            RefreshPathStates();
            Ok($"Copy OK for {profile.Name}");
        });
    });

    public ICommand CmdDeleteLive => new Relay(async _ =>
    {
        var profile = RequireProfile();
        if (System.Windows.MessageBox.Show($"Delete Live for {profile.Name}?", "Confirm",
                System.Windows.MessageBoxButton.YesNo) != System.Windows.MessageBoxResult.Yes)
            return;

        await _copy.DeleteLiveAsync(profile.PathLive);
        RefreshPathStates();
        Ok($"Deleted Live for {profile.Name}");
    });

    public ICommand CmdDeleteCopy => new Relay(async _ =>
    {
        var profile = RequireProfile();
        if (System.Windows.MessageBox.Show($"Delete Copy for {profile.Name}?", "Confirm",
                System.Windows.MessageBoxButton.YesNo) != System.Windows.MessageBoxResult.Yes)
            return;

        await _copy.DeleteCopyAsync(profile.PathCopy);
        RefreshPathStates();
        Ok($"Deleted Copy for {profile.Name}");
    });

    public ICommand CmdRotate => new Relay(async _ =>
    {
        await Guard(async ct =>
        {
            var profile = RequireProfile();
            SyncConfigFromProfile(profile);
            var dst = await _backup.RotateAsync(_shell, _copy,
                profile.PathCopy, profile.PathBackup, profile.PathBackupZip, ct);
            RefreshPathStates();
            Ok($"Backup OK → {dst}");
        });
    });
}

public sealed class Relay : ICommand
{
    readonly Action<object?>? _run;
    readonly Func<bool>? _can;
    readonly Func<object?, Task>? _async;
    public Relay(Action<object?> run, Func<bool>? can = null) { _run = run; _can = can; }
    public Relay(Func<object?, Task> run, Func<bool>? can = null) { _async = run; _can = can; }
    public bool CanExecute(object? p) => _can?.Invoke() ?? true;
    public event EventHandler? CanExecuteChanged;
    public void RiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    public async void Execute(object? p)
    {
        if (_run != null) _run(p);
        else if (_async != null) await _async(p);
    }
}
