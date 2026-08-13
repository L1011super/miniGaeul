using System.Windows;
using GaeulDesktopPet.Services;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace GaeulDesktopPet;

public partial class App : WpfApplication
{
    private MainWindow? _window;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        LogService.Initialize();
        LogService.Info("Application startup");
        try
        {
            _window = new MainWindow();
            _window.Show();
        }
        catch (Exception ex)
        {
            LogService.Error("Startup failed", ex);
            WpfMessageBox.Show(ex.Message, "miniGaeul 启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
