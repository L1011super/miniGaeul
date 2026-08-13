using System.Windows.Threading;
using GaeulDesktopPet.Interop;

namespace GaeulDesktopPet.Services;

public sealed class FullscreenService : IDisposable
{
    private readonly IntPtr _ownHandle;
    private readonly DispatcherTimer _timer;
    private bool _isFullscreen;

    public event Action<bool>? FullscreenChanged;

    public FullscreenService(IntPtr ownHandle)
    {
        _ownHandle = ownHandle;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) => Check();
    }

    public void Start() => _timer.Start();
    public void Dispose() => _timer.Stop();

    private void Check()
    {
        var foreground = NativeMethods.GetForegroundWindow();
        var fullscreen = IsFullscreenOnSameMonitor(foreground);
        if (fullscreen == _isFullscreen) return;
        _isFullscreen = fullscreen;
        LogService.Info($"Fullscreen state changed: {fullscreen}");
        FullscreenChanged?.Invoke(fullscreen);
    }

    private bool IsFullscreenOnSameMonitor(IntPtr foreground)
    {
        if (foreground == IntPtr.Zero || foreground == _ownHandle || NativeMethods.IsIconic(foreground)) return false;
        if (!NativeMethods.GetWindowRect(foreground, out var rect)) return false;
        var fgMonitor = NativeMethods.MonitorFromRect(ref rect, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var ownMonitor = NativeMethods.MonitorFromWindow(_ownHandle, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (fgMonitor != ownMonitor) return false;
        var monitor = ScreenService.GetMonitorInfo(fgMonitor).rcMonitor;
        const int tolerance = 4;
        return Math.Abs(rect.Left - monitor.Left) <= tolerance
            && Math.Abs(rect.Top - monitor.Top) <= tolerance
            && Math.Abs(rect.Right - monitor.Right) <= tolerance
            && Math.Abs(rect.Bottom - monitor.Bottom) <= tolerance;
    }
}
