using System;
using System.Diagnostics;
using System.Windows;
// using System.Windows.Forms; // vermeiden, um Ambiguitäten in WPF zu verhindern
using System.Windows.Threading;

namespace BloodysManager.App
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Binding-Fehler sichtbar machen (Output-Fenster)
            PresentationTraceSources.DataBindingSource.Switch.Level =
                SourceLevels.Critical | SourceLevels.Error;

            // Unhandled-Handler -> damit wir Hinweise bekommen statt „Silent Crash“
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            base.OnStartup(e);
            try
            {
                var vm = new ViewModels.MainViewModel();
                var win = new MainWindow { DataContext = vm };
                win.Show();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString(), "Startup error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1);
            }
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            System.Windows.MessageBox.Show(e.Exception.ToString(), "Unhandled UI exception", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true; // verhindert Sofort-Absturz, damit man Log/Output lesen kann
        }

        private void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            System.Windows.MessageBox.Show(e.ExceptionObject?.ToString() ?? "Unknown error", "Unhandled domain exception",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }

        // Hinweis: Wenn in dieser Datei Application.Current genutzt wird,
        // bitte explizit WPF adressieren:
        // var app = System.Windows.Application.Current;
    }
}
