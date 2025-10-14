using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
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
    private readonly PerfService _perfService;
    private readonly CancellationTokenSource _perfLoopCts = new();

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
            Raise(nameof(SelectedProfile));
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
            }
            return Profiles[ClampIndex(_selectedIndex)];
        }
    }

    public MainViewModel()
    {
        _syncContext = SynchronizationContext.Current ?? new SynchronizationContext();
        _configService = new ConfigService();
        _gitService = new GitService();
        _fileService = new FileOpsService();
        _perfService = new PerfService();

        Config = _configService.Load();
        foreach (var profile in Profiles)
            profile.PropertyChanged += OnProfileChanged;

        _selectedIndex = ClampIndex(Config.SelectedProfileIndex);
        Append("Config loaded.");
        _ = StartPerfLoopAsync(_perfLoopCts.Token);
    }

    private void OnProfileChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ServerProfile profile)
            return;

        if (Profiles.Contains(profile))
        {
            _configService.Save(Config);
            if (profile == SelectedProfile)
                Raise(nameof(SelectedProfile));
        }
    }

    private async Task StartPerfLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var (cpu, ram) = _perfService.Sample();
                Append($"CPU {cpu:F1}% | RAM {ram:F1}%");
                await Task.Delay(TimeSpan.FromSeconds(30), token);
            }
        }
        catch (TaskCanceledException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            Append($"Perf error: {ex.Message}");
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

        profile.LivePath = live;
        profile.CopyPath = copy;
        profile.BackupPath = backup;
        profile.BackupZipPath = backupZip;
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
            _fileService.CopyDirectoryContents(SelectedProfile.LivePath, SelectedProfile.CopyPath, Append);
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
            _fileService.DeleteDirectoryContents(SelectedProfile.LivePath, Append);
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
            _fileService.DeleteDirectoryContents(SelectedProfile.CopyPath, Append);
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
            _fileService.SnapshotToBackup(SelectedProfile.LivePath, SelectedProfile.BackupPath, Append);
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
            _fileService.RotateBackups(SelectedProfile.BackupPath, 5, Append);
        }
        catch (Exception ex)
        {
            Append($"X {ex.Message}");
        }
    }

    public void Dispose()
    {
        _perfLoopCts.Cancel();
        _perfService.Dispose();
        _perfLoopCts.Dispose();
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
