using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using BloodysManager.App.Services;

namespace BloodysManager.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    readonly Config _cfg;
    readonly ShellService _shell = new();
    readonly GitService _git;
    readonly CopyService _copy;
    readonly BackupService _backup;
    readonly ProcessService _proc = new();
    readonly ProcessCpuSampler _cpu = new();

    CancellationTokenSource? _ctsWorld, _ctsAuth;
    bool _runningWorld, _runningAuth;

    public Localizer L { get; }

    public ObservableCollection<ServerProfileVM> Profiles { get; } = new();

    ServerProfileVM? _activeProfile;
    public ServerProfileVM? ActiveProfile
    {
        get => _activeProfile;
        set
        {
            if (value != null && Set(ref _activeProfile, value))
            {
                _activeProfile.OnAnyChanged();
            }
        }
    }

    public MainViewModel(Config cfg)
    {
        _cfg = cfg;
        L = new Localizer(cfg);
        _git = new(_shell, _cfg);
        _copy = new(_shell, _cfg);
        _backup = new(_cfg);

        foreach (var profile in _cfg.ResolveProfiles().Select(p => new ServerProfileVM(p)))
        {
            Profiles.Add(profile);
        }
        ActiveProfile = Profiles.FirstOrDefault();
        RefreshPathStates();
    }

    public string AppTitle => L["AppTitle"];
    public string BtnCreateStruct => L["BtnCreateStructure"];
    public string BtnClean => L["BtnCleanDownload"];
    public string BtnUpdate => L["BtnUpdate"];
    public string BtnCopy => L["BtnCopy"];
    public string BtnDeleteLive => L["BtnDeleteLive"];
    public string BtnDeleteCopy => L["BtnDeleteCopy"];
    public string BtnCurrent => L["BtnCurrent"];
    public string BtnRotate => L["BtnRotate"];
    public string BtnExit => L["BtnExit"];

    void RaiseTextChanges()
    {
        foreach (var name in new[]
        {
            nameof(AppTitle), nameof(BtnCreateStruct), nameof(BtnClean), nameof(BtnUpdate),
            nameof(BtnCopy), nameof(BtnDeleteLive), nameof(BtnDeleteCopy),
            nameof(BtnCurrent), nameof(BtnRotate), nameof(BtnExit)
        })
        {
            Raise(name);
        }
    }

    public ICommand CmdLangDE => new Relay(_ => { L.SetLanguage("de"); RaiseTextChanges(); });
    public ICommand CmdLangEN => new Relay(_ => { L.SetLanguage("en"); RaiseTextChanges(); });

    string _log = string.Empty;
    public string Log
    {
        get => _log;
        set => Set(ref _log, value);
    }

    bool _busy;
    public bool IsBusy
    {
        get => _busy;
        set => Set(ref _busy, value);
    }

    public bool IsWorldRunning
    {
        get => _runningWorld;
        set => Set(ref _runningWorld, value);
    }

    public bool IsAuthRunning
    {
        get => _runningAuth;
        set => Set(ref _runningAuth, value);
    }

    void AppendLog(string prefix, string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {prefix} {message}\n";
        void Apply() => Log += line;
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
            Apply();
        else
            dispatcher.Invoke(Apply);
    }

    void Ok(string s) => AppendLog("✓", s);
    void Err(string s) => AppendLog("✗", s);

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

    void TryCreateDir(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && !Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                Ok($"Ordner erstellt: {path}");
            }
        }
        catch (Exception ex)
        {
            Err($"Ordner erstellen fehlgeschlagen: {ex.Message}");
        }
    }

    void OpenInExplorer(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            var p = Directory.Exists(path) ? path : Path.GetDirectoryName(path ?? string.Empty);
            if (string.IsNullOrWhiteSpace(p) || !Directory.Exists(p))
            {
                Err("Pfad existiert nicht.");
                return;
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{p}\"")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Err($"Explorer öffnen fehlgeschlagen: {ex.Message}");
        }
    }

    void OutWorld(string s) => Ok($"[WORLD] {s}");
    void ErrWorld(string s) => Err($"[WORLD] {s}");
    void OutAuth(string s) => Ok($"[AUTH ] {s}");
    void ErrAuth(string s) => Err($"[AUTH ] {s}");

    void BrowseAssign(Action<string> setter, string desc)
    {
        using var dlg = new FolderBrowserDialog { Description = desc, ShowNewFolderButton = true };
        if (dlg.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
        {
            setter(dlg.SelectedPath);
            RefreshPathStates();
        }
    }

    public void RefreshPathStates()
    {
        foreach (var profile in Profiles)
        {
            profile.OnAnyChanged();
        }
        Raise(nameof(ActiveProfile));
    }

    void SetWorldRunning(bool value)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
            IsWorldRunning = value;
        else
            dispatcher.Invoke(() => IsWorldRunning = value);
    }

    void SetAuthRunning(bool value)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
            IsAuthRunning = value;
        else
            dispatcher.Invoke(() => IsAuthRunning = value);
    }

    public ICommand CmdExit => new Relay(_ => Application.Current?.Shutdown());

    public ICommand CmdSetPathLive => new Relay(_ =>
    {
        if (ActiveProfile is null) return;
        BrowseAssign(v => ActiveProfile.LivePath = v, L["MenuSetPathLive"]);
    });

    public ICommand CmdSetPathCopy => new Relay(_ =>
    {
        if (ActiveProfile is null) return;
        BrowseAssign(v => ActiveProfile.CopyPath = v, L["MenuSetPathCopy"]);
    });

    public ICommand CmdSetPathBackup => new Relay(_ =>
    {
        if (ActiveProfile is null) return;
        BrowseAssign(v => ActiveProfile.BackupRoot = v, L["MenuSetPathBackup"]);
    });

    public ICommand CmdSetPathBackupZip => new Relay(_ =>
    {
        if (ActiveProfile is null) return;
        BrowseAssign(v => ActiveProfile.BackupZipRoot = v, L["MenuSetPathBackupUpdate"]);
    });

    public ICommand CmdOpenPathInExplorer => new Relay(p => OpenInExplorer(p as string));
    public ICommand CmdCreatePathDir => new Relay(p => TryCreateDir(p as string));

    public ICommand CmdOpenSettings => new Relay(_ =>
        System.Windows.MessageBox.Show("Settings placeholder – edit appsettings.json.", L["MenuSettings"]));

    public ICommand CmdOpenCredits => new Relay(_ =>
        System.Windows.MessageBox.Show("BloodysManager – MIT License © 2025", L["MenuCredits"]));

    public ICommand CmdOpenLiveInExplorer => new Relay(_ => OpenInExplorer(ActiveProfile?.LivePath));
    public ICommand CmdOpenCopyInExplorer => new Relay(_ => OpenInExplorer(ActiveProfile?.CopyPath));
    public ICommand CmdOpenBackupInExplorer => new Relay(_ => OpenInExplorer(ActiveProfile?.BackupRoot));

    public ICommand CmdCreateLiveDir => new Relay(_ => TryCreateDir(ActiveProfile?.LivePath));
    public ICommand CmdCreateCopyDir => new Relay(_ => TryCreateDir(ActiveProfile?.CopyPath));
    public ICommand CmdCreateBackupDir => new Relay(_ => TryCreateDir(ActiveProfile?.BackupRoot));

    public ICommand CmdCreateFolders => new Relay(async _ =>
    {
        if (ActiveProfile is null) return;
        await _copy.CreateBaseFoldersAsync(ActiveProfile);
        RefreshPathStates();
        Ok($"[{ActiveProfile.Name}] {L["StatusFoldersCreated"]}");
    });

    public ICommand CmdCleanDownload => new Relay(async _ =>
    {
        if (ActiveProfile is null) return;
        await Guard(async ct =>
        {
            var commit = await _git.CleanCloneAsync(ActiveProfile, ct);
            RefreshPathStates();
            Ok($"[{ActiveProfile.Name}] {string.Format(L["StatusCleanOk"], commit)}");
        });
    });

    public ICommand CmdUpdate => new Relay(async _ =>
    {
        if (ActiveProfile is null) return;
        await Guard(async ct =>
        {
            var commit = await _git.UpdateAsync(ActiveProfile, ct);
            Ok($"[{ActiveProfile.Name}] {string.Format(L["StatusUpdateOk"], commit)}");
        });
    });

    public ICommand CmdCopy => new Relay(async _ =>
    {
        if (ActiveProfile is null) return;
        await Guard(async ct =>
        {
            await _copy.MirrorLiveToCopyAsync(ActiveProfile, ct);
            RefreshPathStates();
            Ok($"[{ActiveProfile.Name}] {L["StatusCopyOk"]}");
        });
    });

    public ICommand CmdCurrent => CmdCopy;

    public ICommand CmdDeleteLive => new Relay(_ =>
    {
        if (ActiveProfile is null) return;
        if (System.Windows.MessageBox.Show(L["DlgConfirmDeleteLive"], "Confirm",
            MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            _copy.DeleteLiveAsync(ActiveProfile).Wait();
            RefreshPathStates();
            Ok($"[{ActiveProfile.Name}] {L["StatusDeletedLive"]}");
        }
    });

    public ICommand CmdDeleteCopy => new Relay(_ =>
    {
        if (ActiveProfile is null) return;
        if (System.Windows.MessageBox.Show(L["DlgConfirmDeleteCopy"], "Confirm",
            MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            _copy.DeleteCopyAsync(ActiveProfile).Wait();
            RefreshPathStates();
            Ok($"[{ActiveProfile.Name}] {L["StatusDeletedCopy"]}");
        }
    });

    public ICommand CmdRotate => new Relay(async _ =>
    {
        if (ActiveProfile is null) return;
        await Guard(async ct =>
        {
            var dst = await _backup.RotateAsync(_shell, _copy, ActiveProfile, ct);
            RefreshPathStates();
            Ok($"[{ActiveProfile.Name}] {string.Format(L["StatusBackupOk"], dst)}");
        });
    });

    public ICommand CmdAllUpdate => new Relay(async _ =>
    {
        await Guard(async ct =>
        {
            foreach (var profile in Profiles)
            {
                ActiveProfile = profile;
                var commit = await _git.UpdateAsync(profile, ct);
                Ok($"[{profile.Name}] {string.Format(L["StatusUpdateOk"], commit)}");
            }
            RefreshPathStates();
        });
    });

    public ICommand CmdAllCopy => new Relay(async _ =>
    {
        await Guard(async ct =>
        {
            foreach (var profile in Profiles)
            {
                ActiveProfile = profile;
                await _copy.MirrorLiveToCopyAsync(profile, ct);
                Ok($"[{profile.Name}] {L["StatusCopyOk"]}");
            }
            RefreshPathStates();
        });
    });

    public ICommand CmdAllRotate => new Relay(async _ =>
    {
        await Guard(async ct =>
        {
            foreach (var profile in Profiles)
            {
                ActiveProfile = profile;
                var dst = await _backup.RotateAsync(_shell, _copy, profile, ct);
                Ok($"[{profile.Name}] {string.Format(L["StatusBackupOk"], dst)}");
            }
            RefreshPathStates();
        });
    });

    public ICommand CmdBrowseWorldExe => new Relay(_ =>
    {
        if (ActiveProfile is null) return;
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Executable (*.exe)|*.exe" };
        if (dlg.ShowDialog() == true)
        {
            ActiveProfile.WorldExePath = dlg.FileName;
            ActiveProfile.OnAnyChanged();
            Raise(nameof(ActiveProfile));
        }
    });

    public ICommand CmdBrowseAuthExe => new Relay(_ =>
    {
        if (ActiveProfile is null) return;
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Executable (*.exe)|*.exe" };
        if (dlg.ShowDialog() == true)
        {
            ActiveProfile.AuthExePath = dlg.FileName;
            ActiveProfile.OnAnyChanged();
            Raise(nameof(ActiveProfile));
        }
    });

    public ICommand CmdStartWorld => new Relay(_ =>
    {
        if (ActiveProfile is null) return;
        if (IsWorldRunning)
        {
            Ok("WORLD läuft bereits.");
            return;
        }
        var exe = ActiveProfile.ResolvedWorldExe;
        if (!File.Exists(exe))
        {
            Err($"WORLD exe nicht gefunden: {exe}");
            return;
        }
        _ctsWorld = new CancellationTokenSource();
        SetWorldRunning(true);
        Ok($"WORLD startet: {exe}");
        _ = Task.Run(async () =>
        {
            var (exit, error) = await _proc.RunAsync(exe, null, Path.GetDirectoryName(exe), OutWorld, ErrWorld, _ctsWorld.Token);
            if (error != null) ErrWorld(error.Message);
            Ok($"WORLD beendet (ExitCode {exit}).");
            SetWorldRunning(false);
        });
    });

    public ICommand CmdStopWorld => new Relay(_ =>
    {
        if (_ctsWorld != null && IsWorldRunning)
        {
            _ctsWorld.Cancel();
            Ok("WORLD: Stop angefordert.");
        }
    });

    public ICommand CmdStartAuth => new Relay(_ =>
    {
        if (ActiveProfile is null) return;
        if (IsAuthRunning)
        {
            Ok("AUTH läuft bereits.");
            return;
        }
        var exe = ActiveProfile.ResolvedAuthExe;
        if (!File.Exists(exe))
        {
            Err($"AUTH exe nicht gefunden: {exe}");
            return;
        }
        _ctsAuth = new CancellationTokenSource();
        SetAuthRunning(true);
        Ok($"AUTH startet: {exe}");
        _ = Task.Run(async () =>
        {
            var (exit, error) = await _proc.RunAsync(exe, null, Path.GetDirectoryName(exe), OutAuth, ErrAuth, _ctsAuth.Token);
            if (error != null) ErrAuth(error.Message);
            Ok($"AUTH beendet (ExitCode {exit}).");
            SetAuthRunning(false);
        });
    });

    public ICommand CmdStopAuth => new Relay(_ =>
    {
        if (_ctsAuth != null && IsAuthRunning)
        {
            _ctsAuth.Cancel();
            Ok("AUTH: Stop angefordert.");
        }
    });

    public ICommand CmdClearLog => new Relay(_ => Log = string.Empty);

    public async Task RefreshProcessCpuAsync()
    {
        try
        {
            if (IsWorldRunning)
            {
                var p = System.Diagnostics.Process.GetProcessesByName("worldserver").FirstOrDefault();
                if (p != null)
                    Ok($"[WORLD] CPU: {await _cpu.SampleCpuPercentAsync(p.Id, 1000):0.0}%");
            }
            if (IsAuthRunning)
            {
                var p = System.Diagnostics.Process.GetProcessesByName("authserver").FirstOrDefault();
                if (p != null)
                    Ok($"[AUTH ] CPU: {await _cpu.SampleCpuPercentAsync(p.Id, 1000):0.0}%");
            }
        }
        catch (Exception ex)
        {
            Err($"CPU-Sample Fehler: {ex.Message}");
        }
    }
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
