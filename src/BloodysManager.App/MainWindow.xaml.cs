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

    private void BrowseLive_Click(object sender, RoutedEventArgs e) => _viewModel.Browse(p => p.LivePath, (p, value) => p.LivePath = value);
    private void BrowseCopy_Click(object sender, RoutedEventArgs e) => _viewModel.Browse(p => p.CopyPath, (p, value) => p.CopyPath = value);
    private void BrowseBackup_Click(object sender, RoutedEventArgs e) => _viewModel.Browse(p => p.BackupPath, (p, value) => p.BackupPath = value);
    private void BrowseBackupZip_Click(object sender, RoutedEventArgs e) => _viewModel.Browse(p => p.BackupZipPath, (p, value) => p.BackupZipPath = value);

    private void CreateStructure_Click(object sender, RoutedEventArgs e) => _viewModel.CreateStructure();
    private async void Download_Click(object sender, RoutedEventArgs e) => await _viewModel.DownloadAsync();
    private async void Update_Click(object sender, RoutedEventArgs e) => await _viewModel.UpdateAsync();
    private async void LiveCopy_Click(object sender, RoutedEventArgs e) => await _viewModel.LiveToCopyAsync();
    private void DeleteLive_Click(object sender, RoutedEventArgs e) => _viewModel.DeleteLive();
    private void DeleteCopy_Click(object sender, RoutedEventArgs e) => _viewModel.DeleteCopy();
    private void Backup_Click(object sender, RoutedEventArgs e) => _viewModel.Backup();
    private void Rotate_Click(object sender, RoutedEventArgs e) => _viewModel.RotateBackup();
}
