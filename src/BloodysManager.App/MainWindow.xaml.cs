namespace BloodysManager.App;

using System.Windows;
using BloodysManager.App.ViewModels;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var cfgPath = System.IO.Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var cfg = Services.Config.Load(cfgPath);

#if !DEBUG
        if (!Services.AdminService.IsElevated())
        {
            // Falls Relaunch klappt, kehren wir aus dem Konstruktor zurück (App fährt gleich runter).
            if (Services.AdminService.TryRelaunchAsAdmin())
                return;
            // Wenn abgebrochen, App läuft ohne Admin weiter.
        }
#endif

        DataContext = new MainViewModel(cfg);

      //  DataContext = new ViewModels.MainViewModel(cfg);
    }

    void EnsureProfileSelected(object? context)
    {
        if (context is ServerProfileVM profile && DataContext is MainViewModel vm && vm.ActiveProfile != profile)
        {
            vm.ActiveProfile = profile;
        }
    }

    void OnProfileElementGotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            EnsureProfileSelected(element.DataContext);
        }
    }

    void OnProfileCommandClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            EnsureProfileSelected(element.DataContext);
        }
    }

    void OnBackupStopClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.IsBusy = false;
            vm.Log += "Backup runner stop requested." + System.Environment.NewLine;
        }
    }
}
