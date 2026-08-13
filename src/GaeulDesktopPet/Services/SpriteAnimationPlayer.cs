using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GaeulDesktopPet.Models;

namespace GaeulDesktopPet.Services;

public sealed class SpriteAnimationPlayer
{
    private readonly string _assetRoot;
    private readonly AnimationFrameCache _cache;
    private readonly DispatcherTimer _timer;
    private AnimationDefinition? _animation;
    private int _frameIndex;
    private bool _repeatCurrentAnimation;

    public event Action<BitmapSource>? FrameChanged;
    public event Action? NonLoopingCompleted;
    public BitmapSource? CurrentFrame { get; private set; }
    public int CurrentFrameIndex => _frameIndex;

    public SpriteAnimationPlayer(string assetRoot, AnimationFrameCache cache)
    {
        _assetRoot = assetRoot;
        _cache = cache;
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => Tick();
    }

    public void Play(AnimationDefinition animation, bool repeat = false)
    {
        _animation = animation;
        _frameIndex = 0;
        _repeatCurrentAnimation = repeat;
        ShowCurrentFrame();
        _timer.Start();
    }

    public void ShowStaticFrame(string fileName)
    {
        if (Path.GetFileName(fileName) != fileName)
            throw new ArgumentException("Static frame must be a file name.", nameof(fileName));

        _timer.Stop();
        _animation = null;
        _frameIndex = 0;
        _repeatCurrentAnimation = false;
        CurrentFrame = _cache.Get(Path.Combine(_assetRoot, fileName));
        FrameChanged?.Invoke(CurrentFrame);
    }

    public void Pause() => _timer.Stop();
    public void Resume() { if (_animation is not null) _timer.Start(); }
    public void Stop() => _timer.Stop();

    private void Tick()
    {
        if (_animation is null) return;
        _frameIndex++;
        if (_frameIndex >= _animation.FrameCount)
        {
            if (_animation.Loop || _repeatCurrentAnimation)
            {
                _frameIndex = 0;
            }
            else
            {
                _timer.Stop();
                NonLoopingCompleted?.Invoke();
                return;
            }
        }

        ShowCurrentFrame();
    }

    private void ShowCurrentFrame()
    {
        if (_animation is null) return;
        _timer.Interval = _animation.GetFrameDuration(_frameIndex);
        var path = Path.Combine(_assetRoot, _animation.GetFrameRelativePath(_frameIndex));
        CurrentFrame = _cache.Get(path);
        FrameChanged?.Invoke(CurrentFrame);
    }
}
