using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
// using System.Windows.Forms; // WPF nutzt keinen WinForms-Dialog
using BloodysManager.App.Models;
using BloodysManager.App.Services;
using Microsoft.Win32;

// WPF: explizit den WPF-Dateidialog verwenden
// Beispiel (Pfad-/Dateiauswahl):
// var dlg = new Microsoft.Win32.OpenFileDialog();
// if (dlg.ShowDialog() == true) { /* dlg.FileName nutzen */ }

// Falls ein Folder-Browser benötigt wird, bitte den
// CommonOpenFileDialog (Windows API Code Pack) oder einen
// eigenen WPF-FolderPicker verwenden (nicht WinForms).

namespace BloodysManager.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    public event PropertyChangedEventHandler? PropertyChanged;
    void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private readonly SynchronizationContext _syncContext;
    private readonly ConfigService _configService;
    private readonly GitService _gitService;
    private readonly FileOpsService _fileService;
    private readonly ProcessService _processService = new();
    private readonly System.Threading.Timer _perfTimer;
    private ProcessPerfSampler? _worldSampler;
    private ProcessPerfSampler? _authSampler;

    public AppConfig Config { get; }
    public ObservableCollection<string> Log { get; } = new();
    public string LogText => string.Join(Environment.NewLine, Log);
    void Append(string message)
    {
        void Write()
        {
            Log.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            while (Log.Count > 500)
                Log.RemoveAt(0);
            Raise(nameof(LogText));
        }

        if (SynchronizationContext.Current == _syncContext)
        {
            Write();
        }
        else
        {
            _syncContext.Post(_ => Write(), null);
        }
    }

    public ObservableCollection<ServerProfile> Profiles => Config.Profiles;

    private void RaiseSelectedProfileChanged()
    {
        Raise(nameof(SelectedProfile));
        Raise(nameof(SelectedProfileExistsLive));
        Raise(nameof(SelectedProfileExistsCopy));
        Raise(nameof(SelectedProfileExistsBackup));
        Raise(nameof(SelectedProfileExistsBackupZip));
    }

    private int _selectedIndex;
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            var normalized = ClampIndex(value);
            if (_selectedIndex == normalized) return;
            _selectedIndex = normalized;
            Config.SelectedProfileIndex = normalized;
            Raise();
            RaiseSelectedProfileChanged();
            _configService.Save(Config);
        }
    }

    private int ClampIndex(int value)
        => Profiles.Count == 0 ? 0 : Math.Clamp(value, 0, Profiles.Count - 1);

    public ServerProfile SelectedProfile
    {
        get
        {
            if (Profiles.Count == 0)
            {
                var profile = new ServerProfile();
                profile.PropertyChanged += OnProfileChanged;
                Profiles.Add(profile);
                _selectedIndex = 0;
                RaiseSelectedProfileChanged();
            }
            return Profiles[ClampIndex(_selectedIndex)];
        }
    }

    public bool SelectedProfileExistsLive
        => Directory.Exists(SelectedProfile.PathLive);

    public bool SelectedProfileExistsCopy
        => Directory.Exists(SelectedProfile.PathCopy);

    public bool SelectedProfileExistsBackup
        => Directory.Exists(SelectedProfile.PathBackup);

    public bool SelectedProfileExistsBackupZip
        => Directory.Exists(SelectedProfile.PathBackupZip);

    private double _worldCpu;
    public double WorldCpu
    {
        get => _worldCpu;
        private set => SetField(ref _worldCpu, value);
    }

    private double _worldRam;
    public double WorldRam
    {
        get => _worldRam;
        private set => SetField(ref _worldRam, value);
    }

    private double _authCpu;
    public double AuthCpu
    {
        get => _authCpu;
        private set => SetField(ref _authCpu, value);
    }

    private double _authRam;
    public double AuthRam
    {
        get => _authRam;
        private set => SetField(ref _authRam, value);
    }

    public string WorldExePath
    {
        get => Config.WorldExePath;
        set
        {
            if (Config.WorldExePath == value)
                return;

            Config.WorldExePath = value ?? string.Empty;
            Raise();
            _configService.Save(Config);
            ResetWorldSampler();
        }
    }

    public string AuthExePath
    {
        get => Config.AuthExePath;
        set
        {
            if (Config.AuthExePath == value)
                return;

            Config.AuthExePath = value ?? string.Empty;
            Raise();
            _configService.Save(Config);
            ResetAuthSampler();
        }
    }

    public MainViewModel()
    {
        _syncContext = SynchronizationContext.Current ?? new SynchronizationContext();
        _configService = new ConfigService();
        _gitService = new GitService();
        _fileService = new FileOpsService();

        Config = _configService.Load();
        foreach (var profile in Profiles)
            profile.PropertyChanged += OnProfileChanged;

        _selectedIndex = ClampIndex(Config.SelectedProfileIndex);
        Append("Config loaded.");

        ResetWorldSampler();
        ResetAuthSampler();
        _perfTimer = new System.Threading.Timer(UpdatePerf, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    private void OnProfileChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ServerProfile profile)
            return;

        if (Profiles.Contains(profile))
        {
            _configService.Save(Config);
            if (profile == SelectedProfile)
                RaiseSelectedProfileChanged();
        }
    }

    private void UpdatePerf(object? state)
    {
        var worldCpu = _worldSampler?.CpuPercent ?? 0.0;
        var worldRam = _worldSampler?.RamMb ?? 0.0;
        var authCpu = _authSampler?.CpuPercent ?? 0.0;
        var authRam = _authSampler?.RamMb ?? 0.0;

        void Apply()
        {
            WorldCpu = worldCpu;
            WorldRam = worldRam;
            AuthCpu = authCpu;
            AuthRam = authRam;
        }

        if (SynchronizationContext.Current == _syncContext)
        {
            Apply();
        }
        else
        {
            _syncContext.Post(_ => Apply(), null);
        }
    }

    public void SaveRepo()
    {
        _configService.Save(Config);
        Append("Repository URL saved.");
    }

    public void SaveDownloadTarget()
    {
        _configService.Save(Config);
        Append("Download target saved.");
    }

    public void NewProfile()
    {
        var number = Profiles.Count + 1;
        var profile = new ServerProfile { Name = $"Server {number}" };
        profile.PropertyChanged += OnProfileChanged;
        Profiles.Add(profile);
        SelectedIndex = Profiles.Count - 1;
        _configService.Save(Config);
        Append($"Profile created: {profile.Name}");
    }

    public void DeleteProfile()
    {
        if (Profiles.Count <= 1)
        {
            Append("Cannot delete last profile.");
            return;
        }

        var profile = SelectedProfile;
        profile.PropertyChanged -= OnProfileChanged;
        var index = SelectedIndex;
        Profiles.Remove(profile);
        SelectedIndex = Math.Clamp(index, 0, Profiles.Count - 1);
        _configService.Save(Config);
        Append($"Profile removed: {profile.Name}");
    }

    public void RenameProfile()
    {
        var current = SelectedProfile;
        using var dialog = new InputBox("Rename Server", current.Name);
        if (dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.Value))
        {
            current.Name = dialog.Value!;
            _configService.Save(Config);
            Append($"Profile renamed to {current.Name}");
        }
    }

    public void Browse(Func<ServerProfile, string> getter, Action<ServerProfile, string> setter)
    {
        var profile = SelectedProfile;
        using var dialog = new FolderBrowserDialog { ShowNewFolderButton = true };
        var initial = getter(profile);
        if (!string.IsNullOrWhiteSpace(initial) && Directory.Exists(initial))
            dialog.SelectedPath = initial;

        if (dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            setter(profile, dialog.SelectedPath);
            _configService.Save(Config);
            Append($"Path updated: {dialog.SelectedPath}");
        }
    }

    public void BrowseWorldExe()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*",
            FileName = string.IsNullOrWhiteSpace(WorldExePath) ? string.Empty : WorldExePath
        };

        if (dialog.ShowDialog() == true)
        {
            WorldExePath = dialog.FileName;
            Append($"World executable set: {dialog.FileName}");
        }
    }

    public void BrowseAuthExe()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*",
            FileName = string.IsNullOrWhiteSpace(AuthExePath) ? string.Empty : AuthExePath
        };

        if (dialog.ShowDialog() == true)
        {
            AuthExePath = dialog.FileName;
            Append($"Auth executable set: {dialog.FileName}");
        }
    }

    public void StartWorld()
        => ExecuteProcessAction(() => _processService.StartExe(WorldExePath, Append));

    public void StopWorld()
        => ExecuteProcessAction(() => _processService.StopByPath(WorldExePath, Append));

    public void RestartWorld()
        => ExecuteProcessAction(() => _processService.Restart(WorldExePath, Append));

    public void StartAuth()
        => ExecuteProcessAction(() => _processService.StartExe(AuthExePath, Append));

    public void StopAuth()
        => ExecuteProcessAction(() => _processService.StopByPath(AuthExePath, Append));

    public void RestartAuth()
        => ExecuteProcessAction(() => _processService.Restart(AuthExePath, Append));

    private void ExecuteProcessAction(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Append($"X {ex.Message}");
        }
    }

    private void ResetWorldSampler()
    {
        _worldSampler?.Dispose();
        _worldSampler = null;

        if (!string.IsNullOrWhiteSpace(WorldExePath) && File.Exists(WorldExePath))
        {
            _worldSampler = new ProcessPerfSampler(WorldExePath, TimeSpan.FromSeconds(5));
            _worldSampler.Start();
        }

        UpdatePerf(null);
    }

    private void ResetAuthSampler()
    {
        _authSampler?.Dispose();
        _authSampler = null;

        if (!string.IsNullOrWhiteSpace(AuthExePath) && File.Exists(AuthExePath))
        {
            _authSampler = new ProcessPerfSampler(AuthExePath, TimeSpan.FromSeconds(5));
            _authSampler.Start();
        }

        UpdatePerf(null);
    }

    public void CreateStructure()
    {
        var profile = SelectedProfile;
        using var dialog = new FolderBrowserDialog
        {
            ShowNewFolderButton = true,
            Description = "Choose root folder for server"
        };

        if (dialog.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
            return;

        var rootName = profile.Name.Replace(' ', '_');
        var root = Path.Combine(dialog.SelectedPath, rootName);
        var live = Path.Combine(root, "Live");
        var copy = Path.Combine(root, "Copy");
        var backup = Path.Combine(root, "Backup");
        var backupZip = Path.Combine(root, "BackupZip");

        _fileService.EnsureDir(live);
        _fileService.EnsureDir(copy);
        _fileService.EnsureDir(backup);
        _fileService.EnsureDir(backupZip);

        profile.PathLive = live;
        profile.PathCopy = copy;
        profile.PathBackup = backup;
        profile.PathBackupZip = backupZip;
        _configService.Save(Config);
        Append($"Structure created under: {root}");
    }

    public async Task DownloadAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Config.RepositoryUrl))
                throw new InvalidOperationException("Repository URL is empty.");
            if (string.IsNullOrWhiteSpace(Config.DownloadTarget))
                throw new InvalidOperationException("Download target is empty.");

            Directory.CreateDirectory(Config.DownloadTarget);
            if (Directory.Exists(Path.Combine(Config.DownloadTarget, ".git")))
            {
                Append("Target already contains a repository – use Update.");
                return;
            }

            await _gitService.CloneAsync(Config.RepositoryUrl, Config.DownloadTarget, Append);
        }
        catch (Exception ex)
        {
            Append($"X {ex.Message}");
        }
    }

    public async Task UpdateAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Config.RepositoryUrl))
            {
                Append("Repository URL is empty.");
                return;
            }
            var gitDir = Path.Combine(Config.DownloadTarget, ".git");
            if (!Directory.Exists(gitDir))
            {
                Append("No repository found in DownloadTarget.");
                return;
            }

            await _gitService.PullAsync(Config.DownloadTarget, Append);
        }
        catch (Exception ex)
        {
            Append($"X {ex.Message}");
        }
    }

    public async Task LiveToCopyAsync()
    {
        try
        {
            _fileService.CopyDirectoryContents(SelectedProfile.PathLive, SelectedProfile.PathCopy, Append);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Append($"X {ex.Message}");
        }
    }

    public void DeleteLive()
    {
        try
        {
            _fileService.DeleteDirectoryContents(SelectedProfile.PathLive, Append);
        }
        catch (Exception ex)
        {
            Append($"X {ex.Message}");
        }
    }

    public void DeleteCopy()
    {
        try
        {
            _fileService.DeleteDirectoryContents(SelectedProfile.PathCopy, Append);
        }
        catch (Exception ex)
        {
            Append($"X {ex.Message}");
        }
    }

    public void Backup()
    {
        try
        {
            _fileService.SnapshotToBackup(SelectedProfile.PathLive, SelectedProfile.PathBackup, Append);
        }
        catch (Exception ex)
        {
            Append($"X {ex.Message}");
        }
    }

    public void RotateBackup()
    {
        try
        {
            _fileService.RotateBackups(SelectedProfile.PathBackup, 5, Append);
        }
        catch (Exception ex)
        {
            Append($"X {ex.Message}");
        }
    }

    public void Dispose()
    {
        _perfTimer.Dispose();
        _worldSampler?.Dispose();
        _authSampler?.Dispose();
        foreach (var profile in Profiles)
            profile.PropertyChanged -= OnProfileChanged;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        Raise(propertyName);
        return true;
    }
}

public sealed class InputBox : Form
{
    public string? Value => _textBox.Text;
    private readonly TextBox _textBox = new() { Dock = DockStyle.Top };

    public InputBox(string title, string initial)
    {
        Text = title;
        Width = 400;
        Height = 140;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;

        Controls.Add(_textBox);
        _textBox.Text = initial;

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom,
            Padding = new Padding(10),
            Height = 50
        };

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 80 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };
        buttonPanel.Controls.Add(ok);
        buttonPanel.Controls.Add(cancel);

        Controls.Add(buttonPanel);
        AcceptButton = ok;
        CancelButton = cancel;
    }
}
