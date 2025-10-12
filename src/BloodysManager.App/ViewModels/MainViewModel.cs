using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Forms;
using BloodysManager.App.Services;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BloodysManager.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    void Set<T>(ref T field, T value, [CallerMemberName] string? n = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new(n));
    }

    readonly Config _cfg;
    readonly ShellService _shell = new();
    readonly GitService _git;
    readonly CopyService _copy;
    readonly BackupService _backup;

    public Localizer L { get; }

    public MainViewModel(Config cfg)
    {
        _cfg = cfg;
        L = new Localizer(cfg);
        _git = new(_shell, _cfg);
        _copy = new(_shell, _cfg);
        _backup = new(_cfg);

        PathDownload = _cfg.LivePath;
        PathLive     = _cfg.LivePath;
        PathCopy     = _cfg.CopyPath;
        PathBackup   = _cfg.BackupRoot;
        PathBackupUpdate = _cfg.BackupRoot;

        RefreshPathStates();
    }

    public string AppTitle        => "Bloody's Manager";
    public string BtnCreateStruct => "Ordner-Struktur erstellen";
    public string BtnClean        => "1) Download/Update";
    public string BtnUpdate       => "1) Update";
    public string BtnCopy         => "2) Live → Copy";
    public string BtnDeleteLive   => "3) Delete Live";
    public string BtnDeleteCopy   => "3.1) Delete Copy";
    public string BtnCurrent      => "5) Rotate → Backup";
    public string BtnRotate       => "5) Rotate → Backup";
    public string BtnExit         => "9) Exit";

    void RaiseTextChanges()
    {
        foreach (var name in new[]{
            nameof(AppTitle), nameof(BtnCreateStruct), nameof(BtnClean), nameof(BtnUpdate),
            nameof(BtnCopy), nameof(BtnDeleteLive), nameof(BtnDeleteCopy),
            nameof(BtnCurrent), nameof(BtnRotate), nameof(BtnExit)
        }) PropertyChanged?.Invoke(this, new(name));
    }

    public ICommand CmdCreateFolders => new Relay(async _ =>
    {
        using var dlg = new FolderBrowserDialog { Description = "Root-Ordner wählen für Live/Copy/Backup", ShowNewFolderButton = true };
        if (dlg.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
        {
            await _copy.CreateBaseFoldersAsync(dlg.SelectedPath);
            PathLive   = _cfg.LivePath;
            PathCopy   = _cfg.CopyPath;
            PathBackup = _cfg.BackupRoot;
            PathDownload = _cfg.LivePath;
            _cfg.Save(AppContext.BaseDirectory);
            RefreshPathStates();
            Ok($"Ordner angelegt: {dlg.SelectedPath}");
        }
    });

    string _pathDownload="", _pathLive="", _pathCopy="", _pathBackup="", _pathBackupUpdate="";
    public string PathDownload
    {
        get=>_pathDownload;
        set
        {
            Set(ref _pathDownload, value);
            OnPathsChanged();
        }
    }
    public string PathLive
    {
        get=>_pathLive;
        set
        {
            Set(ref _pathLive, value);
            _cfg.LivePath = value;
            OnPathsChanged();
        }
    }
    public string PathCopy
    {
        get=>_pathCopy;
        set
        {
            Set(ref _pathCopy, value);
            _cfg.CopyPath = value;
            OnPathsChanged();
        }
    }
    public string PathBackup
    {
        get=>_pathBackup;
        set
        {
            Set(ref _pathBackup, value);
            _cfg.BackupRoot = value;
            OnPathsChanged();
        }
    }
    public string PathBackupUpdate
    {
        get=>_pathBackupUpdate;
        set
        {
            Set(ref _pathBackupUpdate, value);
            OnPathsChanged();
        }
    }

    bool _existsDownload, _existsLive, _existsCopy, _existsBackup, _existsBackupUpdate;
    public bool ExistsDownload { get=>_existsDownload; set=>Set(ref _existsDownload, value); }
    public bool ExistsLive     { get=>_existsLive;     set=>Set(ref _existsLive, value); }
    public bool ExistsCopy     { get=>_existsCopy;     set=>Set(ref _existsCopy, value); }
    public bool ExistsBackup   { get=>_existsBackup;   set=>Set(ref _existsBackup, value); }
    public bool ExistsBackupUpdate { get=>_existsBackupUpdate; set=>Set(ref _existsBackupUpdate, value); }

    void OnPathsChanged()=>RefreshPathStates();
    void RefreshPathStates()
    {
        ExistsDownload     = Directory.Exists(PathDownload);
        ExistsLive         = Directory.Exists(PathLive);
        ExistsCopy         = Directory.Exists(PathCopy);
        ExistsBackup       = Directory.Exists(PathBackup);
        ExistsBackupUpdate = Directory.Exists(PathBackupUpdate);
    }

    string _log=""; public string Log { get=>_log; set=>Set(ref _log, value); }
    bool _busy; public bool IsBusy { get=>_busy; set=>Set(ref _busy, value); }

    void Ok(string s)=>Log += $"[{DateTime.Now:HH:mm:ss}] ✓ {s}\n";
    void Err(string s)=>Log+= $"[{DateTime.Now:HH:mm:ss}] ✗ {s}\n";

    async Task Guard(Func<CancellationToken, Task> op)
    {
        try { IsBusy = true; await op(CancellationToken.None); }
        catch (Exception ex) { Err(ex.Message); }
        finally { IsBusy = false; }
    }

    public ICommand CmdExit => new Relay(_ => System.Windows.Application.Current.Shutdown());

    public ICommand CmdOpenSettings => new Relay(_ =>
        System.Windows.MessageBox.Show("Settings placeholder – edit appsettings.json.", "Settings"));
    public ICommand CmdOpenCredits => new Relay(_ =>
        System.Windows.MessageBox.Show("Bloody's Manager – MIT License © 2025", "Credits"));

    public ICommand CmdCleanDownload => new Relay(async _ =>
    {
        await Guard(async ct =>
        {
            var commit = await _git.CleanCloneAsync(ct);
            RefreshPathStates();
            Ok($"Download/Update OK (commit {commit})");
        });
    });

    public ICommand CmdUpdate => new Relay(async _ =>
    {
        await Guard(async ct =>
        {
            var commit = await _git.UpdateAsync(ct);
            Ok($"Update OK (commit {commit})");
        });
    });

    public ICommand CmdCopy => new Relay(async _ =>
    {
        await Guard(async ct => { await _copy.MirrorLiveToCopyAsync(ct); RefreshPathStates(); Ok("Copy OK"); });
    });

    public ICommand CmdDeleteLive => new Relay(_ =>
    {
        if (System.Windows.MessageBox.Show("Delete Live?", "Confirm",
            System.Windows.MessageBoxButton.YesNo)==System.Windows.MessageBoxResult.Yes)
        { _copy.DeleteLiveAsync().Wait(); RefreshPathStates(); Ok("Deleted Live"); }
    });

    public ICommand CmdDeleteCopy => new Relay(_ =>
    {
        if (System.Windows.MessageBox.Show("Delete Copy?", "Confirm",
            System.Windows.MessageBoxButton.YesNo)==System.Windows.MessageBoxResult.Yes)
        { _copy.DeleteCopyAsync().Wait(); RefreshPathStates(); Ok("Deleted Copy"); }
    });

    public ICommand CmdRotate => new Relay(async _ =>
    {
        await Guard(async ct =>
        {
            var dst = await _backup.RotateAsync(_shell, _copy, ct);
            RefreshPathStates();
            Ok($"Backup OK → {dst}");
        });
    });

    // === Vorbereitung für mehrere Server (TODO in Folgeschritt) ===
    // public ObservableCollection<ServerProfileVM> Profiles { get; } = new();
}

public sealed class Relay : ICommand
{
    readonly Action<object?>? _run;
    readonly Func<bool>? _can;
    readonly Func<object?, Task>? _async;
    public Relay(Action<object?> run, Func<bool>? can = null) { _run = run; _can = can; }
    public Relay(Func<object?, Task> run, Func<bool>? can = null) { _async = run; _can = can; }
    public bool CanExecute(object? p) => _can?.Invoke() ?? true;
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public async void Execute(object? p)
    {
        if (_run != null) _run(p);
        else if (_async != null) await _async(p ?? new object());
    }
}
