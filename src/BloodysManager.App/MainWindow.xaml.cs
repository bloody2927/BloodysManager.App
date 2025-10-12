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

        var git = new GitService(cfg);
        var copy = new CopyService(cfg);
        var backup = new BackupService(cfg);

        DataContext = new MainViewModel(cfg, git, copy, backup);
    }
}
