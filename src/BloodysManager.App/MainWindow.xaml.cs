using System;
using System.Windows;
using System.Windows.Controls;
using BloodysManager.App.ViewModels;

namespace BloodysManager.App;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel =>
        DataContext as MainViewModel ?? throw new InvalidOperationException("MainViewModel is not set.");

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (DataContext is MainViewModel vm)
        {
            vm.Dispose();
        }
    }

    private void LogBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.ScrollToEnd();
        }
    }

    private void SaveRepo_Click(object sender, RoutedEventArgs e) => ViewModel.SaveRepo();
    private void SaveDownload_Click(object sender, RoutedEventArgs e) => ViewModel.SaveDownloadTarget();

    private void NewProfile_Click(object sender, RoutedEventArgs e) => ViewModel.NewProfile();
    private void DeleteProfile_Click(object sender, RoutedEventArgs e) => ViewModel.DeleteProfile();
    private void RenameProfile_Click(object sender, RoutedEventArgs e) => ViewModel.RenameProfile();

    private void BrowseLive_Click(object sender, RoutedEventArgs e) => ViewModel.Browse(p => p.PathLive, (p, value) => p.PathLive = value);
    private void BrowseCopy_Click(object sender, RoutedEventArgs e) => ViewModel.Browse(p => p.PathCopy, (p, value) => p.PathCopy = value);
    private void BrowseBackup_Click(object sender, RoutedEventArgs e) => ViewModel.Browse(p => p.PathBackup, (p, value) => p.PathBackup = value);
    private void BrowseBackupZip_Click(object sender, RoutedEventArgs e) => ViewModel.Browse(p => p.PathBackupZip, (p, value) => p.PathBackupZip = value);

    private void CreateStructure_Click(object sender, RoutedEventArgs e) => ViewModel.CreateStructure();
    private async void Download_Click(object sender, RoutedEventArgs e) => await ViewModel.DownloadAsync();
    private async void Update_Click(object sender, RoutedEventArgs e) => await ViewModel.UpdateAsync();
    private async void LiveCopy_Click(object sender, RoutedEventArgs e) => await ViewModel.LiveToCopyAsync();
    private void DeleteLive_Click(object sender, RoutedEventArgs e) => ViewModel.DeleteLive();
    private void DeleteCopy_Click(object sender, RoutedEventArgs e) => ViewModel.DeleteCopy();
    private void Backup_Click(object sender, RoutedEventArgs e) => ViewModel.Backup();
    private void Rotate_Click(object sender, RoutedEventArgs e) => ViewModel.RotateBackup();

    private void BrowseWorldExe_Click(object sender, RoutedEventArgs e) => ViewModel.BrowseWorldExe();
    private void BrowseAuthExe_Click(object sender, RoutedEventArgs e) => ViewModel.BrowseAuthExe();
    private void StartWorld_Click(object sender, RoutedEventArgs e) => ViewModel.StartWorld();
    private void RestartWorld_Click(object sender, RoutedEventArgs e) => ViewModel.RestartWorld();
    private void StopWorld_Click(object sender, RoutedEventArgs e) => ViewModel.StopWorld();
    private void StartAuth_Click(object sender, RoutedEventArgs e) => ViewModel.StartAuth();
    private void RestartAuth_Click(object sender, RoutedEventArgs e) => ViewModel.RestartAuth();
    private void StopAuth_Click(object sender, RoutedEventArgs e) => ViewModel.StopAuth();
}
