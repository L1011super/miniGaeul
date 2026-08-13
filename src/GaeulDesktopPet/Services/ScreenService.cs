using System.Windows;
using System.Windows.Interop;
using GaeulDesktopPet.Interop;
using Forms = System.Windows.Forms;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;
using WpfSize = System.Windows.Size;

namespace GaeulDesktopPet.Services;

public static class ScreenService
{
    public static double CalculateDefaultPetDipSize(Window window, double scale)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var screen = Forms.Screen.FromHandle(handle);
        var physical = Math.Max(96, screen.Bounds.Width / 12.0) * scale * 2.0;
        var dpi = NativeMethods.GetDpiForWindow(handle);
        return physical * 96.0 / Math.Max(96, dpi);
    }

    public static void PlaceAtDefault(Window window, double sizeDip)
    {
        var screen = Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens.First();
        var scale = 96.0 / Math.Max(96, NativeMethods.GetDpiForWindow(new WindowInteropHelper(window).Handle));
        window.Left = screen.WorkingArea.Right * scale - sizeDip - 24 * scale;
        window.Top = screen.WorkingArea.Bottom * scale - sizeDip - 24 * scale;
    }

    public static void ClampToVisibleScreen(Window window)
    {
        var screen = Forms.Screen.FromRectangle(new System.Drawing.Rectangle((int)window.Left, (int)window.Top, (int)window.Width, (int)window.Height));
        var area = screen.WorkingArea;
        window.Left = Math.Clamp(window.Left, area.Left - window.Width + 48, area.Right - 48);
        window.Top = Math.Clamp(window.Top, area.Top - window.Height + 48, area.Bottom - 48);
    }

    public static bool MoveWithinWorkArea(
        Window window,
        int horizontalPixels,
        int verticalPixels,
        int edgeMarginPixels = 12)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero ||
            !NativeMethods.GetWindowRect(handle, out var bounds) ||
            bounds.Width <= 0)
        {
            return false;
        }

        var workArea = Forms.Screen.FromHandle(handle).WorkingArea;
        var nextPosition = CalculateMovementPosition(
            new WpfRect(bounds.Left, bounds.Top, bounds.Width, bounds.Height),
            new WpfRect(workArea.Left, workArea.Top, workArea.Width, workArea.Height),
            horizontalPixels,
            verticalPixels,
            edgeMarginPixels);
        if (Math.Abs(nextPosition.X - bounds.Left) < 0.001 &&
            Math.Abs(nextPosition.Y - bounds.Top) < 0.001)
        {
            return false;
        }

        return NativeMethods.SetWindowPos(
            handle,
            IntPtr.Zero,
            (int)Math.Round(nextPosition.X),
            (int)Math.Round(nextPosition.Y),
            0,
            0,
            NativeMethods.SWP_NOSIZE |
            NativeMethods.SWP_NOZORDER |
            NativeMethods.SWP_NOACTIVATE);
    }

    public static WpfPoint CalculateMovementPosition(
        WpfRect windowBounds,
        WpfRect workArea,
        double horizontalDistance,
        double verticalDistance,
        double edgeMargin)
    {
        var margin = Math.Max(0, edgeMargin);
        var minimumLeft = workArea.Left + margin;
        var maximumLeft = Math.Max(minimumLeft, workArea.Right - margin - windowBounds.Width);
        var minimumTop = workArea.Top + margin;
        var maximumTop = Math.Max(minimumTop, workArea.Bottom - margin - windowBounds.Height);
        return new WpfPoint(
            Math.Clamp(windowBounds.Left + horizontalDistance, minimumLeft, maximumLeft),
            Math.Clamp(windowBounds.Top + verticalDistance, minimumTop, maximumTop));
    }

    public static void PlaceAdjacent(Window window, Window anchor, int gapPixels = 100)
    {
        var anchorHandle = new WindowInteropHelper(anchor).Handle;
        var physicalToDip = 96.0 / Math.Max(96, NativeMethods.GetDpiForWindow(anchorHandle));
        var gapDip = gapPixels * physicalToDip;
        var targetSize = new WpfSize(
            ResolveWindowDimension(window.ActualWidth, window.Width),
            ResolveWindowDimension(window.ActualHeight, window.Height));
        var anchorBounds = new WpfRect(
            anchor.Left,
            anchor.Top,
            ResolveWindowDimension(anchor.ActualWidth, anchor.Width),
            ResolveWindowDimension(anchor.ActualHeight, anchor.Height));
        if (!NativeMethods.GetWindowRect(anchorHandle, out var anchorRect))
        {
            var screen = Forms.Screen.FromHandle(anchorHandle);
            var fallbackWorkArea = new WpfRect(
                screen.WorkingArea.Left * physicalToDip,
                screen.WorkingArea.Top * physicalToDip,
                screen.WorkingArea.Width * physicalToDip,
                screen.WorkingArea.Height * physicalToDip);
            targetSize = FitWindowHeightToWorkArea(window, targetSize, fallbackWorkArea);
            var fallbackPosition = CalculateAdjacentPosition(
                anchorBounds,
                fallbackWorkArea,
                targetSize,
                gapDip);
            window.Left = fallbackPosition.X;
            window.Top = fallbackPosition.Y;
            return;
        }

        var screenForAnchor = Forms.Screen.FromHandle(anchorHandle);
        var physicalWorkArea = screenForAnchor.WorkingArea;
        var windowHandle = new WindowInteropHelper(window).Handle;
        if (windowHandle != IntPtr.Zero &&
            NativeMethods.GetWindowRect(windowHandle, out var windowRect) &&
            windowRect.Width > 0 &&
            windowRect.Height > 0)
        {
            var physicalPosition = CalculateAdjacentPosition(
                new WpfRect(anchorRect.Left, anchorRect.Top, anchorRect.Width, anchorRect.Height),
                new WpfRect(physicalWorkArea.Left, physicalWorkArea.Top, physicalWorkArea.Width, physicalWorkArea.Height),
                new WpfSize(windowRect.Width, windowRect.Height),
                gapPixels);
            var left = (int)Math.Round(physicalPosition.X);
            var top = (int)Math.Round(physicalPosition.Y);
            var placed = NativeMethods.SetWindowPos(
                windowHandle,
                IntPtr.Zero,
                left,
                top,
                0,
                0,
                NativeMethods.SWP_NOSIZE |
                NativeMethods.SWP_NOZORDER |
                NativeMethods.SWP_NOACTIVATE);
            if (placed)
            {
                LogService.Info(
                    $"Settings initial placement: anchor=({anchorRect.Left},{anchorRect.Top},{anchorRect.Width},{anchorRect.Height}), " +
                    $"panel=({windowRect.Width},{windowRect.Height}), work=({physicalWorkArea.Left},{physicalWorkArea.Top},{physicalWorkArea.Width},{physicalWorkArea.Height}), " +
                    $"result=({left},{top})");
                return;
            }

            LogService.Warn("Native settings placement failed; falling back to WPF positioning.");
        }

        var workArea = new WpfRect(
            anchor.Left + (physicalWorkArea.Left - anchorRect.Left) * physicalToDip,
            anchor.Top + (physicalWorkArea.Top - anchorRect.Top) * physicalToDip,
            physicalWorkArea.Width * physicalToDip,
            physicalWorkArea.Height * physicalToDip);
        targetSize = FitWindowHeightToWorkArea(window, targetSize, workArea);
        var position = CalculateAdjacentPosition(
            anchorBounds, workArea, targetSize, gapDip);
        window.Left = position.X;
        window.Top = position.Y;
    }

    public static WpfPoint CalculateAdjacentPosition(
        WpfRect anchorBounds,
        WpfRect workArea,
        WpfSize targetSize,
        double gap)
    {
        var availableLeft = anchorBounds.Left - workArea.Left;
        var availableRight = workArea.Right - anchorBounds.Right;
        var requiredSideSpace = gap + targetSize.Width;
        var placeOnRight = availableLeft < requiredSideSpace ||
                           availableRight >= requiredSideSpace;
        var x = placeOnRight
            ? anchorBounds.Right + gap
            : anchorBounds.Left - gap - targetSize.Width;
        var bottomAlignedTop = Math.Max(workArea.Top, workArea.Bottom - targetSize.Height);
        var y = anchorBounds.Top < workArea.Top
            ? workArea.Top
            : anchorBounds.Top + targetSize.Height > workArea.Bottom
                ? bottomAlignedTop
                : anchorBounds.Top;
        return new WpfPoint(x, y);
    }

    private static WpfSize FitWindowHeightToWorkArea(
        Window window,
        WpfSize targetSize,
        WpfRect workArea)
    {
        if (workArea.Height <= 0 || targetSize.Height <= workArea.Height) return targetSize;
        window.Height = workArea.Height;
        return new WpfSize(targetSize.Width, workArea.Height);
    }

    private static double ResolveWindowDimension(double actual, double configured)
    {
        if (double.IsFinite(actual) && actual > 0) return actual;
        if (double.IsFinite(configured) && configured > 0) return configured;
        return 0;
    }

    public static NativeMethods.MONITORINFOEX GetMonitorInfo(IntPtr monitor)
    {
        var info = new NativeMethods.MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFOEX>(), szDevice = string.Empty };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            LogService.Warn($"GetMonitorInfoW failed with Win32 error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}.");
        }
        return info;
    }
}
