using System.Windows;
using BloodysManager.App.Services;
using BloodysManager.App.ViewModels;

namespace BloodysManager.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var cfg = Config.Load(AppContext.BaseDirectory);

#if !DEBUG
        if (!AdminService.IsElevated())
        {
            if (AdminService.TryRelaunchAsAdmin())
                return;
        }
#endif

        DataContext = new MainViewModel(cfg);
    }
}
