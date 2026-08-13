using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using GaeulDesktopPet.Interop;
using GaeulDesktopPet.Models;
using GaeulDesktopPet.Services;
using FormsSystemInformation = System.Windows.Forms.SystemInformation;
using WpfApplication = System.Windows.Application;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;

namespace GaeulDesktopPet;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly AnimationFrameCache _cache = new();
    private readonly SpriteAnimationPlayer _player;
    private readonly RecentActionPicker _actionPicker = new();
    private readonly RandomActionScheduler _scheduler = new();
    private readonly TrayIconService _tray = new();
    private readonly PetSettings _settings;
    private FullscreenService? _fullscreen;
    private HwndSource? _source;
    private Views.SettingsWindow? _settingsWindow;
    private PetState _state = PetState.Loading;
    private CancellationTokenSource? _singleClickCts;
    private WpfPoint _mouseDownPoint;
    private WpfPoint _dragWindowOrigin;
    private bool _dragging;
    private bool _returningFromDrag;
    private bool _rightWalking;
    private int _dragDirection;
    private int _walkVerticalDirection;
    private double _walkHorizontalRemainder;
    private double _walkVerticalRemainder;
    private const double WalkPixelsPerFrame = 8.8;

    public MainWindow()
    {
        InitializeComponent();
        var assetRoot = AnimationCatalog.ResolveAssetRoot();
        AnimationCatalog.ValidateAssets(assetRoot);
        _settings = _settingsService.Load();
        _player = new SpriteAnimationPlayer(assetRoot, _cache);
        WireServices();

        Loaded += OnLoaded;
        Closing += (_, e) =>
        {
            if (_state == PetState.Exiting) return;
            e.Cancel = true;
            HidePet();
        };
    }

    private void WireServices()
    {
        _player.FrameChanged += OnFrameChanged;
        _player.NonLoopingCompleted += OnNonLoopingCompleted;
        _scheduler.Due += () => Dispatcher.Invoke(() =>
        {
            if (_state == PetState.Idle && !_dragging && !_returningFromDrag) PlayRandomInteraction();
            _scheduler.Restart(_settings);
        });
        _tray.ShowHideRequested += ToggleHidden;
        _tray.SettingsRequested += OpenSettings;
        _tray.ToggleAutoRequested += () =>
        {
            _settings.InteractionFrequency =
                _settings.InteractionFrequency == InteractionFrequencyLevel.Off
                    ? InteractionFrequencyLevel.Often
                    : InteractionFrequencyLevel.Off;
            SaveSettings();
            if (_state == PetState.Idle) _scheduler.Restart(_settings);
            else _scheduler.Stop();
        };
        _tray.ExitRequested += ExitApplication;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var exStyle = NativeMethods.GetWindowLong(handle, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(handle, NativeMethods.GWL_EXSTYLE, exStyle | NativeMethods.WS_EX_TOOLWINDOW);

        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(WndProc);
        _fullscreen = new FullscreenService(handle);
        _fullscreen.FullscreenChanged += OnFullscreenChanged;
        _fullscreen.Start();

        ApplySize();
        if (_settings.Left.HasValue && _settings.Top.HasValue)
        {
            Left = _settings.Left.Value;
            Top = _settings.Top.Value;
            ScreenService.ClampToVisibleScreen(this);
        }
        else
        {
            ScreenService.PlaceAtDefault(this, Width);
        }

        if (_settings.Hidden) HidePet(); else EnterDefaultAnimation();
    }

    private void ApplySize()
    {
        var size = ScreenService.CalculateDefaultPetDipSize(this, _settings.SizeScale);
        Width = Height = size;
        DragRotate.CenterX = DragRotate.CenterY = size / 2;
    }

    private void EnterIdle()
    {
        if (_state is PetState.SettingsOpen or PetState.Hidden or PetState.SuspendedByFullscreen or PetState.Exiting) return;
        _state = PetState.Idle;
        PlayAnimation(AnimationCatalog.Idle);
        _scheduler.Restart(_settings);
    }

    private void EnterDefaultAnimation()
    {
        if (_returningFromDrag ||
            _state is PetState.SettingsOpen or PetState.Hidden or PetState.SuspendedByFullscreen or PetState.Exiting)
        {
            return;
        }

        if (GetConfiguredContinuousAction() is { } animation)
        {
            _state = PetState.Interaction;
            _scheduler.Stop();
            PlayAnimation(animation, repeat: true);
            return;
        }

        EnterIdle();
    }

    private AnimationDefinition? GetConfiguredContinuousAction() =>
        _settings.ContinuousActionEnabled
            ? AnimationCatalog.FindSettingsAction(_settings.SelectedInteractionName)
            : null;

    private void OnNonLoopingCompleted()
    {
        if (_rightWalking)
        {
            _rightWalking = false;
            SavePosition();
        }

        if (_settingsWindow is not null && _state == PetState.SettingsOpen)
        {
            if (IsVisible && !_settings.Hidden) ShowPausedIdleFrame();
            return;
        }

        EnterDefaultAnimation();
    }

    private void PlayRandomInteraction()
    {
        _state = PetState.Interaction;
        _scheduler.Stop();
        PlayAnimation(_actionPicker.Pick(AnimationCatalog.Interactions));
    }

    private void OnLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_returningFromDrag ||
            _state is PetState.SettingsOpen or PetState.SuspendedByFullscreen or PetState.Hidden or PetState.Exiting)
        {
            return;
        }

        CancelPendingClick();
        _mouseDownPoint = PointToScreenDip(e.GetPosition(this));
        _dragWindowOrigin = new WpfPoint(Left, Top);
        _dragging = false;
        _dragDirection = 0;
        CaptureMouse();
    }

    private void OnMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || !IsMouseCaptured) return;
        var current = PointToScreenDip(e.GetPosition(this));
        var dx = current.X - _mouseDownPoint.X;
        var dy = current.Y - _mouseDownPoint.Y;
        if (!_dragging)
        {
            if (Math.Abs(dx) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(dy) < SystemParameters.MinimumVerticalDragDistance) return;
            CancelPendingClick();
            ClearDragTransformAnimations();
            _dragging = true;
            _scheduler.Stop();
        }

        UpdateDragFrame(dx);
        Left = _dragWindowOrigin.X + dx;
        Top = _dragWindowOrigin.Y + dy;
        DragRotate.Angle = Math.Clamp(dx / 12.0, -10, 10);
        DragTranslate.Y = Math.Clamp(Math.Abs(dx + dy) / 30.0, 0, 4);
    }

    private async void OnLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_returningFromDrag) return;
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (_dragging)
        {
            _dragging = false;
            _dragDirection = 0;
            ScreenService.ClampToVisibleScreen(this);
            SavePosition();
            _returningFromDrag = true;
            try
            {
                await AnimateDragTransformsBackAsync();
            }
            finally
            {
                _returningFromDrag = false;
            }

            if (_state is PetState.Hidden or PetState.SuspendedByFullscreen or PetState.Exiting) return;
            if (_settingsWindow is { } dialog && _state == PetState.SettingsOpen)
            {
                ResumeSettingsAnimation(dialog);
            }
            else
            {
                EnterDefaultAnimation();
            }
            return;
        }

        if (IsVisiblePixel(e.GetPosition(this))) QueueSingleClick();
    }

    private void UpdateDragFrame(double horizontalOffset)
    {
        var direction = Math.Sign(horizontalOffset);
        if (direction == 0 || direction == _dragDirection) return;
        _dragDirection = direction;
        if (_rightWalking) SavePosition();
        _rightWalking = false;
        _player.ShowStaticFrame(direction < 0
            ? AnimationCatalog.DragLeftFrame
            : AnimationCatalog.DragRightFrame);
    }

    private void ClearDragTransformAnimations()
    {
        DragRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null);
        DragTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);
    }

    private Task AnimateDragTransformsBackAsync()
    {
        var startAngle = DragRotate.Angle;
        var startOffsetY = DragTranslate.Y;
        ClearDragTransformAnimations();
        DragRotate.Angle = 0;
        DragTranslate.Y = 0;

        var duration = TimeSpan.FromMilliseconds(150);
        var angleAnimation = new DoubleAnimation(startAngle, 0, duration)
        {
            FillBehavior = FillBehavior.Stop
        };
        var offsetAnimation = new DoubleAnimation(startOffsetY, 0, duration)
        {
            FillBehavior = FillBehavior.Stop
        };
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var remainingAnimations = 2;

        void OnAnimationCompleted(object? sender, EventArgs e)
        {
            remainingAnimations--;
            if (remainingAnimations != 0) return;
            angleAnimation.Completed -= OnAnimationCompleted;
            offsetAnimation.Completed -= OnAnimationCompleted;
            completion.TrySetResult(true);
        }

        angleAnimation.Completed += OnAnimationCompleted;
        offsetAnimation.Completed += OnAnimationCompleted;
        DragRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, angleAnimation);
        DragTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, offsetAnimation);
        return completion.Task;
    }

    private async void QueueSingleClick()
    {
        CancelPendingClick();
        var clickCts = new CancellationTokenSource();
        _singleClickCts = clickCts;
        try
        {
            await Task.Delay(FormsSystemInformation.DoubleClickTime, clickCts.Token);
            if (!clickCts.IsCancellationRequested &&
                _state is not (PetState.SettingsOpen or PetState.SuspendedByFullscreen or PetState.Hidden or PetState.Exiting))
            {
                PlayRandomInteraction();
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (ReferenceEquals(_singleClickCts, clickCts)) _singleClickCts = null;
            clickCts.Dispose();
        }
    }

    private void OnRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        CancelPendingClick();
        OpenSettings();
    }

    private void OpenSettings()
    {
        if (_state == PetState.Exiting) return;
        if (_settingsWindow is { } existingDialog)
        {
            if (!existingDialog.IsVisible) existingDialog.Show();
            existingDialog.Activate();
            return;
        }

        _state = PetState.SettingsOpen;
        _player.Pause();
        _scheduler.Stop();

        var dialog = new Views.SettingsWindow(_settings);
        _settingsWindow = dialog;
        if (IsVisible) dialog.Owner = this;
        dialog.Loaded += (_, _) => ScreenService.PlaceAdjacent(dialog, this);
        dialog.SettingsChanged += (_, e) => ApplyLiveSettings(dialog, e.Kind);
        dialog.InteractionPlaybackRequested += (_, e) =>
            PlaySettingsInteraction(dialog, e.Animation, e.Continuous);
        dialog.ExitRequested += (_, _) => ExitApplication();
        dialog.Closed += (_, _) =>
        {
            _settingsWindow = null;
            if (_state == PetState.Exiting) return;
            if (_settings.Hidden)
            {
                _state = PetState.Hidden;
                return;
            }

            _state = PetState.Loading;
            EnterDefaultAnimation();
        };
        dialog.Show();
        ShowPausedIdleFrame();
    }

    private void ApplyLiveSettings(Views.SettingsWindow dialog, Views.SettingsChangeKind kind)
    {
        switch (kind)
        {
            case Views.SettingsChangeKind.Size:
                ApplySize();
                ScreenService.ClampToVisibleScreen(this);
                break;
            case Views.SettingsChangeKind.Startup:
                StartupService.SetEnabled(_settings.StartWithWindows);
                break;
            case Views.SettingsChangeKind.Visibility:
                if (_settings.Hidden)
                {
                    dialog.Owner = null;
                    _player.Pause();
                    _scheduler.Stop();
                    Hide();
                }
                else
                {
                    if (!IsVisible) Show();
                    dialog.Owner = this;
                    ResumeSettingsAnimation(dialog);
                }

                _state = PetState.SettingsOpen;
                break;
            case Views.SettingsChangeKind.Position:
                ScreenService.PlaceAtDefault(this, Width);
                break;
        }

        SaveSettings();
    }

    private void PlaySettingsInteraction(
        Views.SettingsWindow dialog,
        AnimationDefinition animation,
        bool continuous)
    {
        if (!ReferenceEquals(_settingsWindow, dialog) ||
            !dialog.IsVisible ||
            !IsVisible ||
            _returningFromDrag ||
            _settings.Hidden ||
            _state is PetState.SuspendedByFullscreen or PetState.Exiting)
        {
            return;
        }

        _state = PetState.SettingsOpen;
        _scheduler.Stop();
        PlayAnimation(animation, continuous);
        SaveSettings();
    }

    private void ResumeSettingsAnimation(Views.SettingsWindow dialog)
    {
        if (_returningFromDrag) return;
        _state = PetState.SettingsOpen;
        _scheduler.Stop();
        if (dialog.SelectedInteraction is { } animation)
        {
            PlayAnimation(animation, dialog.ContinuousActionEnabled);
        }
        else
        {
            ShowPausedIdleFrame();
        }
    }

    private void ShowPausedIdleFrame()
    {
        PlayAnimation(AnimationCatalog.Idle);
        _player.Pause();
    }

    private void PlayAnimation(AnimationDefinition animation, bool repeat = false)
    {
        var willWalkRight = string.Equals(
            animation.Name,
            AnimationCatalog.MoveRight.Name,
            StringComparison.OrdinalIgnoreCase);
        if (_rightWalking && !willWalkRight) SavePosition();
        _rightWalking = willWalkRight;
        if (willWalkRight)
        {
            _walkVerticalDirection = Random.Shared.Next(2) == 0 ? -1 : 1;
            _walkHorizontalRemainder = 0;
            _walkVerticalRemainder = 0;
        }
        _player.Play(animation, repeat);
    }

    private void OnFrameChanged(System.Windows.Media.Imaging.BitmapSource bitmap)
    {
        SpriteImage.Source = bitmap;
        if (!_rightWalking ||
            !AnimationCatalog.IsMoveRightTravelFrame(_player.CurrentFrameIndex) ||
            _state is PetState.Hidden or PetState.SuspendedByFullscreen or PetState.Exiting)
        {
            return;
        }

        var radians = Math.PI / 6;
        _walkHorizontalRemainder += WalkPixelsPerFrame * Math.Cos(radians);
        _walkVerticalRemainder += _walkVerticalDirection * WalkPixelsPerFrame * Math.Sin(radians);
        var horizontalPixels = (int)Math.Truncate(_walkHorizontalRemainder);
        var verticalPixels = (int)Math.Truncate(_walkVerticalRemainder);
        _walkHorizontalRemainder -= horizontalPixels;
        _walkVerticalRemainder -= verticalPixels;
        ScreenService.MoveWithinWorkArea(this, horizontalPixels, verticalPixels);
    }

    private void OnFullscreenChanged(bool fullscreen)
    {
        if (!_settings.SuppressInFullscreen) return;
        var handle = new WindowInteropHelper(this).Handle;
        if (fullscreen)
        {
            _state = PetState.SuspendedByFullscreen;
            _player.Pause();
            _scheduler.Stop();
            NativeMethods.SetWindowPos(handle, NativeMethods.HWND_NOTOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        }
        else
        {
            NativeMethods.SetWindowPos(handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            if (_settingsWindow is { } dialog)
            {
                if (IsVisible && !_settings.Hidden) ResumeSettingsAnimation(dialog);
                return;
            }

            _state = PetState.Loading;
            EnterDefaultAnimation();
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_NCHITTEST)
        {
            var x = unchecked((short)((long)lParam & 0xffff));
            var y = unchecked((short)(((long)lParam >> 16) & 0xffff));
            var client = PointFromScreen(new WpfPoint(x, y));
            handled = true;
            return new IntPtr(IsVisiblePixel(client) ? NativeMethods.HTCLIENT : NativeMethods.HTTRANSPARENT);
        }

        if (msg == NativeMethods.WM_DPICHANGED) ApplySize();
        return IntPtr.Zero;
    }

    private bool IsVisiblePixel(WpfPoint point)
    {
        if (_player.CurrentFrame is null || Width <= 0 || Height <= 0) return true;
        var px = (int)Math.Floor(point.X / Width * _player.CurrentFrame.PixelWidth);
        var py = (int)Math.Floor(point.Y / Height * _player.CurrentFrame.PixelHeight);
        return _cache.GetAlpha(_player.CurrentFrame, px, py) >= 15;
    }

    private WpfPoint PointToScreenDip(WpfPoint local)
    {
        var screen = PointToScreen(local);
        var source = PresentationSource.FromVisual(this);
        return source?.CompositionTarget?.TransformFromDevice.Transform(screen) ?? screen;
    }

    private void ToggleHidden()
    {
        if (IsVisible) HidePet(); else ShowPet();
    }

    private void HidePet()
    {
        _state = PetState.Hidden;
        _settings.Hidden = true;
        if (_settingsWindow is { } dialog) dialog.Owner = null;
        _player.Pause();
        _scheduler.Stop();
        SaveSettings();
        Hide();
    }

    private void ShowPet()
    {
        _settings.Hidden = false;
        SaveSettings();
        Show();
        if (_settingsWindow is { } dialog)
        {
            dialog.Owner = this;
            ResumeSettingsAnimation(dialog);
            return;
        }

        _state = PetState.Loading;
        EnterDefaultAnimation();
    }

    private void SavePosition()
    {
        _settings.Left = Left;
        _settings.Top = Top;
        SaveSettings();
    }

    private void SaveSettings() => _settingsService.Save(_settings);

    private void CancelPendingClick()
    {
        _singleClickCts?.Cancel();
        _singleClickCts?.Dispose();
        _singleClickCts = null;
    }

    public void ExitApplication()
    {
        _state = PetState.Exiting;
        CancelPendingClick();
        SavePosition();
        _fullscreen?.Dispose();
        _scheduler.Dispose();
        _tray.Dispose();
        _player.Stop();
        _source?.RemoveHook(WndProc);
        WpfApplication.Current.Shutdown();
    }
}
