namespace BloodysManager.App;
using BloodysManager.App.ViewModels;

public partial class MainWindow : System.Windows.Window
{
    public MainWindow()
    {
        InitializeComponent();

        var cfgPath = System.IO.Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var cfg = Services.Config.Load(cfgPath);

#if !DEBUG
        if (!Services.AdminService.IsElevated())
        {
            // Falls Relaunch klappt, kehren wir aus dem Konstruktor zur�ck (App f�hrt gleich runter).
            if (Services.AdminService.TryRelaunchAsAdmin())
                return;
            // Wenn abgebrochen, App l�uft ohne Admin weiter.
        }
#endif

        DataContext = new BloodysManager.App.ViewModels.MainViewModel(cfg);

      //  DataContext = new ViewModels.MainViewModel(cfg);
    }
}
