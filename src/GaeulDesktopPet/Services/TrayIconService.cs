using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using WpfApplication = System.Windows.Application;

namespace GaeulDesktopPet.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Forms.NotifyIcon _icon;
    private readonly Drawing.Icon _trayIcon;

    public event Action? ShowHideRequested;
    public event Action? SettingsRequested;
    public event Action? ToggleAutoRequested;
    public event Action? ExitRequested;

    public TrayIconService()
    {
        _menu = BuildMenu();
        _trayIcon = LoadTrayIcon();
        _icon = new Forms.NotifyIcon
        {
            Icon = _trayIcon,
            Text = "miniGaeul 桌面宠物",
            Visible = true,
            ContextMenuStrip = _menu
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) ShowHideRequested?.Invoke();
        };
    }

    private static Drawing.Icon LoadTrayIcon()
    {
        var resource = WpfApplication.GetResourceStream(
            new Uri("pack://application:,,,/Assets/Icon/app.ico"))
            ?? throw new InvalidOperationException("Tray icon resource is missing.");
        using var stream = resource.Stream;
        using var icon = new Drawing.Icon(stream);
        return (Drawing.Icon)icon.Clone();
    }

    private Forms.ContextMenuStrip BuildMenu()
    {
        var menu = new Forms.ContextMenuStrip
        {
            Font = new Drawing.Font("Microsoft YaHei UI", 9F)
        };
        menu.Items.Add("显示/隐藏", null, (_, _) => ShowHideRequested?.Invoke());
        menu.Items.Add("设置", null, (_, _) => SettingsRequested?.Invoke());
        menu.Items.Add("暂停/恢复自动互动", null, (_, _) => ToggleAutoRequested?.Invoke());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitRequested?.Invoke());
        return menu;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Icon = null;
        _icon.Dispose();
        _trayIcon.Dispose();
        _menu.Dispose();
    }
}
