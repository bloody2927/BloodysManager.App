using BloodysManager.App.ViewModels;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Forms;
using BloodysManager.App.Services;
using System.IO;

namespace BloodysManager.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    void Set<T>(ref T field, T value, [CallerMemberName] string? n = null)
    { field = value; PropertyChanged?.Invoke(this, new(n)); }

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

    public string AppTitle        => L["AppTitle"];
    public string BtnCreateStruct => L["BtnCreateStructure"];
    public string BtnClean        => L["BtnCleanDownload"];
    public string BtnUpdate       => L["BtnUpdate"];
    public string BtnCopy         => L["BtnCopy"];
    public string BtnDeleteLive   => L["BtnDeleteLive"];
    public string BtnDeleteCopy   => L["BtnDeleteCopy"];
    public string BtnCurrent      => L["BtnCurrent"];
    public string BtnRotate       => L["BtnRotate"];
    public string BtnExit         => L["BtnExit"];

    void RaiseTextChanges()
    {
        foreach (var name in new[]{
            nameof(AppTitle), nameof(BtnCreateStruct), nameof(BtnClean), nameof(BtnUpdate),
            nameof(BtnCopy), nameof(BtnDeleteLive), nameof(BtnDeleteCopy),
            nameof(BtnCurrent), nameof(BtnRotate), nameof(BtnExit)
        }) PropertyChanged?.Invoke(this, new(name));
    }

    public ICommand CmdLangDE => new Relay(_ => { L.SetLanguage("de"); RaiseTextChanges(); });
    public ICommand CmdLangEN => new Relay(_ => { L.SetLanguage("en"); RaiseTextChanges(); });

    string _pathDownload="", _pathLive="", _pathCopy="", _pathBackup="", _pathBackupUpdate="";
    public string PathDownload { get=>_pathDownload; set { Set(ref _pathDownload, value); OnPathsChanged(); } }
    public string PathLive     { get=>_pathLive;     set { Set(ref _pathLive, value); OnPathsChanged(); } }
    public string PathCopy     { get=>_pathCopy;     set { Set(ref _pathCopy, value); OnPathsChanged(); } }
    public string PathBackup   { get=>_pathBackup;   set { Set(ref _pathBackup, value); OnPathsChanged(); } }
    public string PathBackupUpdate { get=>_pathBackupUpdate; set { Set(ref _pathBackupUpdate, value); OnPathsChanged(); } }

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

    public ICommand CmdSetPathDownload     => new Relay(_=>BrowseAssign(v=>PathDownload=v, L["MenuSetPathDownload"]));
    public ICommand CmdSetPathLive         => new Relay(_=>BrowseAssign(v=>PathLive=v,     L["MenuSetPathLive"]));
    public ICommand CmdSetPathCopy         => new Relay(_=>BrowseAssign(v=>PathCopy=v,     L["MenuSetPathCopy"]));
    public ICommand CmdSetPathBackup       => new Relay(_=>BrowseAssign(v=>PathBackup=v,   L["MenuSetPathBackup"]));
    public ICommand CmdSetPathBackupUpdate => new Relay(_=>BrowseAssign(v=>PathBackupUpdate=v, L["MenuSetPathBackupUpdate"]));
    public ICommand CmdExit => new Relay(_ => System.Windows.Application.Current.Shutdown());

    void BrowseAssign(Action<string> setter, string desc)
    {
        using var dlg = new FolderBrowserDialog { Description = desc, ShowNewFolderButton = true };
        if (dlg.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
        { setter(dlg.SelectedPath); RefreshPathStates(); }
    }

    public ICommand CmdOpenSettings => new Relay(_ =>
        System.Windows.MessageBox.Show("Settings placeholder – edit appsettings.json.", L["MenuSettings"]));
    public ICommand CmdOpenCredits => new Relay(_ =>
        System.Windows.MessageBox.Show("BloodysManager – MIT License © 2025", L["MenuCredits"]));

    public ICommand CmdCreateFolders => new Relay(async _ =>
    {
        await _copy.CreateBaseFoldersAsync();
        RefreshPathStates();
        Ok(L["StatusFoldersCreated"]);
    });

    public ICommand CmdCleanDownload => new Relay(async _ =>
    {
        await Guard(async ct =>
        {
            var commit = await _git.CleanCloneAsync(ct);
            RefreshPathStates();
            Ok(string.Format(L["StatusCleanOk"], commit));
        });
    });

    public ICommand CmdUpdate => new Relay(async _ =>
    {
        await Guard(async ct =>
        {
            var commit = await _git.UpdateAsync(ct);
            Ok(string.Format(L["StatusUpdateOk"], commit));
        });
    });

    public ICommand CmdCopy => new Relay(async _ =>
    {
        await Guard(async ct => { await _copy.MirrorLiveToCopyAsync(ct); RefreshPathStates(); Ok(L["StatusCopyOk"]); });
    });

    public ICommand CmdDeleteLive => new Relay(_ =>
    {
        if (System.Windows.MessageBox.Show(L["DlgConfirmDeleteLive"], "Confirm",
            System.Windows.MessageBoxButton.YesNo)==System.Windows.MessageBoxResult.Yes)
        { _copy.DeleteLiveAsync().Wait(); RefreshPathStates(); Ok(L["StatusDeletedLive"]); }
    });

    public ICommand CmdDeleteCopy => new Relay(_ =>
    {
        if (System.Windows.MessageBox.Show(L["DlgConfirmDeleteCopy"], "Confirm",
            System.Windows.MessageBoxButton.YesNo)==System.Windows.MessageBoxResult.Yes)
        { _copy.DeleteCopyAsync().Wait(); RefreshPathStates(); Ok(L["StatusDeletedCopy"]); }
    });

    public ICommand CmdCurrent => new Relay(async _ =>
    {
        await Guard(async ct => { await _copy.MirrorLiveToCopyAsync(ct); RefreshPathStates(); Ok(L["StatusCopyOk"]); });
    });

    public ICommand CmdRotate => new Relay(async _ =>
    {
        await Guard(async ct =>
        {
            var dst = await _backup.RotateAsync(_shell, _copy, ct);
            RefreshPathStates();
            Ok(string.Format(L["StatusBackupOk"], dst));
        });
    });
}

public sealed class Relay : ICommand
{
    readonly Action<object?>? _run;
    readonly Func<bool>? _can;
    readonly Func<object?, Task>? _async;
    public Relay(Action<object?> run, Func<bool>? can=null){ _run=run; _can=can; }
    public Relay(Func<object?, Task> run, Func<bool>? can=null){ _async=run; _can=can; }
    public bool CanExecute(object? p)=>_can?.Invoke() ?? true;
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public async void Execute(object? p){ if(_run!=null)_run(p); else if(_async!=null)await _async(p??new()); }
}
