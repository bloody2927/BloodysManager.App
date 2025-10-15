using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using BloodysManager.App.Models;
using BloodysManager.App.Services;

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
    private ProcessPerfSampler? _worldSampler;
    private ProcessPerfSampler? _authSampler;
    private CancellationTokenSource? _monitorCts;
    private Task? _monitorTask;

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
        Raise(nameof(SelectedWorldExeExists));
        Raise(nameof(SelectedAuthExeExists));
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
            StartProcessMonitoring();
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

    public bool SelectedWorldExeExists
    {
        get
        {
            var path = GetWorldExePath();
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        }
    }

    public bool SelectedAuthExeExists
    {
        get
        {
            var path = GetAuthExePath();
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        }
    }

    double _worldCpu;
    public double WorldCpu
    {
        get => _worldCpu;
        private set
        {
            if (Math.Abs(_worldCpu - value) > 0.01)
            {
                _worldCpu = value;
                Raise();
            }
        }
    }

    double _worldRam;
    public double WorldRam
    {
        get => _worldRam;
        private set
        {
            if (Math.Abs(_worldRam - value) > 0.1)
            {
                _worldRam = value;
                Raise();
            }
        }
    }

    double _authCpu;
    public double AuthCpu
    {
        get => _authCpu;
        private set
        {
            if (Math.Abs(_authCpu - value) > 0.01)
            {
                _authCpu = value;
                Raise();
            }
        }
    }

    double _authRam;
    public double AuthRam
    {
        get => _authRam;
        private set
        {
            if (Math.Abs(_authRam - value) > 0.1)
            {
                _authRam = value;
                Raise();
            }
        }
    }

    bool _worldRunning;
    public bool WorldRunning
    {
        get => _worldRunning;
        private set
        {
            if (_worldRunning != value)
            {
                _worldRunning = value;
                Raise();
                Raise(nameof(WorldStatus));
            }
        }
    }

    bool _authRunning;
    public bool AuthRunning
    {
        get => _authRunning;
        private set
        {
            if (_authRunning != value)
            {
                _authRunning = value;
                Raise();
                Raise(nameof(AuthStatus));
            }
        }
    }

    public string WorldStatus => WorldRunning ? "Running" : "Stopped";

    public string AuthStatus => AuthRunning ? "Running" : "Stopped";

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
        RaiseSelectedProfileChanged();
        StartProcessMonitoring();
    }

    private void OnProfileChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ServerProfile profile)
            return;

        if (Profiles.Contains(profile))
        {
            _configService.Save(Config);
            if (profile == SelectedProfile)
            {
                RaiseSelectedProfileChanged();
                if (string.IsNullOrEmpty(e.PropertyName)
                    || e.PropertyName == nameof(ServerProfile.PathLive)
                    || e.PropertyName == nameof(ServerProfile.WorldExePath)
                    || e.PropertyName == nameof(ServerProfile.AuthExePath))
                {
                    StartProcessMonitoring();
                }
            }
        }
    }

    private void StartProcessMonitoring()
    {
        _monitorCts?.Cancel();
        _monitorCts = new CancellationTokenSource();

        _worldSampler?.Dispose();
        _worldSampler = null;
        _authSampler?.Dispose();
        _authSampler = null;

        var worldPath = GetWorldExePath();
        if (!string.IsNullOrWhiteSpace(worldPath))
        {
            _worldSampler = new ProcessPerfSampler(worldPath, TimeSpan.FromSeconds(5));
            _worldSampler.Start();
        }

        var authPath = GetAuthExePath();
        if (!string.IsNullOrWhiteSpace(authPath))
        {
            _authSampler = new ProcessPerfSampler(authPath, TimeSpan.FromSeconds(5));
            _authSampler.Start();
        }

        UpdateWorldMetrics();
        UpdateAuthMetrics();

        var token = _monitorCts.Token;
        _monitorTask = Task.Run(() => MonitorLoopAsync(token), token);
    }

    private async Task MonitorLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                UpdateWorldMetrics();
                UpdateAuthMetrics();
                await Task.Delay(TimeSpan.FromSeconds(1), token);
            }
        }
        catch (TaskCanceledException)
        {
            // expected during shutdown
        }
    }

    private void UpdateWorldMetrics()
    {
        WorldCpu = _worldSampler?.CpuPercent ?? 0;
        WorldRam = _worldSampler?.RamMb ?? 0;
        var path = GetWorldExePath();
        WorldRunning = !string.IsNullOrWhiteSpace(path) && ProcessService.IsRunning(path);
        Raise(nameof(SelectedWorldExeExists));
    }

    private void UpdateAuthMetrics()
    {
        AuthCpu = _authSampler?.CpuPercent ?? 0;
        AuthRam = _authSampler?.RamMb ?? 0;
        var path = GetAuthExePath();
        AuthRunning = !string.IsNullOrWhiteSpace(path) && ProcessService.IsRunning(path);
        Raise(nameof(SelectedAuthExeExists));
    }

    private string GetWorldExePath()
    {
        var profile = SelectedProfile;
        if (!string.IsNullOrWhiteSpace(profile.WorldExePath))
            return profile.WorldExePath;
        if (!string.IsNullOrWhiteSpace(profile.PathLive))
            return Path.Combine(profile.PathLive, "bin", "worldserver.exe");
        return string.Empty;
    }

    private string GetAuthExePath()
    {
        var profile = SelectedProfile;
        if (!string.IsNullOrWhiteSpace(profile.AuthExePath))
            return profile.AuthExePath;
        if (!string.IsNullOrWhiteSpace(profile.PathLive))
            return Path.Combine(profile.PathLive, "bin", "authserver.exe");
        return string.Empty;
    }

    private bool EnsureExePath(string path, string label, bool requireExists)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Append($"{label} executable path is not configured.");
            return false;
        }

        if (requireExists && !File.Exists(path))
        {
            Append($"{label} executable not found: {path}");
            return false;
        }

        return true;
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

    public void BrowseWorldExe()
    {
        var profile = SelectedProfile;
        var dialog = new OpenFileDialog
        {
            Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*",
            FileName = string.IsNullOrWhiteSpace(profile.WorldExePath) ? string.Empty : profile.WorldExePath
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FileName))
        {
            profile.WorldExePath = dialog.FileName;
            _configService.Save(Config);
            Append($"World executable set: {dialog.FileName}");
            StartProcessMonitoring();
        }
    }

    public void BrowseAuthExe()
    {
        var profile = SelectedProfile;
        var dialog = new OpenFileDialog
        {
            Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*",
            FileName = string.IsNullOrWhiteSpace(profile.AuthExePath) ? string.Empty : profile.AuthExePath
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FileName))
        {
            profile.AuthExePath = dialog.FileName;
            _configService.Save(Config);
            Append($"Auth executable set: {dialog.FileName}");
            StartProcessMonitoring();
        }
    }

    public void StartWorld()
    {
        var path = GetWorldExePath();
        if (!EnsureExePath(path, "World server", requireExists: true))
            return;

        try
        {
            _processService.StartExe(path, Append);
        }
        catch (Exception ex)
        {
            Append($"X Failed to start world server: {ex.Message}");
        }

        StartProcessMonitoring();
    }

    public void StopWorld()
    {
        var path = GetWorldExePath();
        if (!EnsureExePath(path, "World server", requireExists: false))
            return;

        try
        {
            if (!_processService.StopByPath(path, Append))
                Append("World server was not running.");
        }
        catch (Exception ex)
        {
            Append($"X Failed to stop world server: {ex.Message}");
        }

        StartProcessMonitoring();
    }

    public void RestartWorld()
    {
        var path = GetWorldExePath();
        if (!EnsureExePath(path, "World server", requireExists: true))
            return;

        try
        {
            _processService.Restart(path, Append);
        }
        catch (Exception ex)
        {
            Append($"X Failed to restart world server: {ex.Message}");
        }

        StartProcessMonitoring();
    }

    public void StartAuth()
    {
        var path = GetAuthExePath();
        if (!EnsureExePath(path, "Auth server", requireExists: true))
            return;

        try
        {
            _processService.StartExe(path, Append);
        }
        catch (Exception ex)
        {
            Append($"X Failed to start auth server: {ex.Message}");
        }

        StartProcessMonitoring();
    }

    public void StopAuth()
    {
        var path = GetAuthExePath();
        if (!EnsureExePath(path, "Auth server", requireExists: false))
            return;

        try
        {
            if (!_processService.StopByPath(path, Append))
                Append("Auth server was not running.");
        }
        catch (Exception ex)
        {
            Append($"X Failed to stop auth server: {ex.Message}");
        }

        StartProcessMonitoring();
    }

    public void RestartAuth()
    {
        var path = GetAuthExePath();
        if (!EnsureExePath(path, "Auth server", requireExists: true))
            return;

        try
        {
            _processService.Restart(path, Append);
        }
        catch (Exception ex)
        {
            Append($"X Failed to restart auth server: {ex.Message}");
        }

        StartProcessMonitoring();
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
            StartProcessMonitoring();
        }
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
        StartProcessMonitoring();
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
        _monitorCts?.Cancel();
        try
        {
            _monitorTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // ignore
        }

        _monitorCts?.Dispose();
        _worldSampler?.Dispose();
        _authSampler?.Dispose();
        foreach (var profile in Profiles)
            profile.PropertyChanged -= OnProfileChanged;
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
