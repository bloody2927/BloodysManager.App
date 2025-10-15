using System;
using System.Windows;
using BloodysManager.App.ViewModels;

namespace BloodysManager.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _viewModel.Dispose();
    }

    private void SaveRepo_Click(object sender, RoutedEventArgs e) => _viewModel.SaveRepo();
    private void SaveDownload_Click(object sender, RoutedEventArgs e) => _viewModel.SaveDownloadTarget();

    private void NewProfile_Click(object sender, RoutedEventArgs e) => _viewModel.NewProfile();
    private void DeleteProfile_Click(object sender, RoutedEventArgs e) => _viewModel.DeleteProfile();
    private void RenameProfile_Click(object sender, RoutedEventArgs e) => _viewModel.RenameProfile();

    private void BrowseLive_Click(object sender, RoutedEventArgs e) => _viewModel.Browse(p => p.PathLive, (p, value) => p.PathLive = value);
    private void BrowseCopy_Click(object sender, RoutedEventArgs e) => _viewModel.Browse(p => p.PathCopy, (p, value) => p.PathCopy = value);
    private void BrowseBackup_Click(object sender, RoutedEventArgs e) => _viewModel.Browse(p => p.PathBackup, (p, value) => p.PathBackup = value);
    private void BrowseBackupZip_Click(object sender, RoutedEventArgs e) => _viewModel.Browse(p => p.PathBackupZip, (p, value) => p.PathBackupZip = value);

    private void CreateStructure_Click(object sender, RoutedEventArgs e) => _viewModel.CreateStructure();
    private async void Download_Click(object sender, RoutedEventArgs e) => await _viewModel.DownloadAsync();
    private async void Update_Click(object sender, RoutedEventArgs e) => await _viewModel.UpdateAsync();
    private async void LiveCopy_Click(object sender, RoutedEventArgs e) => await _viewModel.LiveToCopyAsync();
    private void DeleteLive_Click(object sender, RoutedEventArgs e) => _viewModel.DeleteLive();
    private void DeleteCopy_Click(object sender, RoutedEventArgs e) => _viewModel.DeleteCopy();
    private void Backup_Click(object sender, RoutedEventArgs e) => _viewModel.Backup();
    private void Rotate_Click(object sender, RoutedEventArgs e) => _viewModel.RotateBackup();

    private void BrowseWorldExe_Click(object sender, RoutedEventArgs e) => _viewModel.BrowseWorldExe();
    private void BrowseAuthExe_Click(object sender, RoutedEventArgs e) => _viewModel.BrowseAuthExe();
    private void StartWorld_Click(object sender, RoutedEventArgs e) => _viewModel.StartWorld();
    private void RestartWorld_Click(object sender, RoutedEventArgs e) => _viewModel.RestartWorld();
    private void StopWorld_Click(object sender, RoutedEventArgs e) => _viewModel.StopWorld();
    private void StartAuth_Click(object sender, RoutedEventArgs e) => _viewModel.StartAuth();
    private void RestartAuth_Click(object sender, RoutedEventArgs e) => _viewModel.RestartAuth();
    private void StopAuth_Click(object sender, RoutedEventArgs e) => _viewModel.StopAuth();
}
