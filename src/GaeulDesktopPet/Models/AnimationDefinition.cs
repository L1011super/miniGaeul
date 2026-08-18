namespace GaeulDesktopPet.Models;

public sealed record AnimationDefinition(
    string Name,
    bool Loop,
    int Fps,
    int FrameCount,
    IReadOnlyList<int>? SourceFrameIndices = null,
    IReadOnlyList<int>? FrameHoldCounts = null,
    string? AssetDirectory = null,
    Func<IReadOnlyList<TimeSpan>>? FrameDurationFactory = null)
{
    public string GetFrameFileName(int index)
    {
        if ((uint)index >= (uint)FrameCount) throw new ArgumentOutOfRangeException(nameof(index));
        ValidateCustomFrames();
        var sourceIndex = SourceFrameIndices?[index] ?? index;
        return $"{Name}_f{sourceIndex:D3}.png";
    }

    public string GetFrameRelativePath(int index) =>
        string.IsNullOrWhiteSpace(AssetDirectory)
            ? GetFrameFileName(index)
            : Path.Combine(AssetDirectory, GetFrameFileName(index));

    public TimeSpan GetFrameDuration(int index)
    {
        if ((uint)index >= (uint)FrameCount) throw new ArgumentOutOfRangeException(nameof(index));
        ValidateCustomFrames();
        var holdCount = FrameHoldCounts?[index] ?? 1;
        return TimeSpan.FromMilliseconds(1000.0 * holdCount / Fps);
    }

    public IReadOnlyList<TimeSpan> CreateFrameDurations()
    {
        ValidateCustomFrames();
        var durations = FrameDurationFactory?.Invoke()
            ?? Enumerable.Range(0, FrameCount).Select(GetFrameDuration).ToArray();

        if (durations.Count != FrameCount || durations.Any(duration => duration <= TimeSpan.Zero))
        {
            throw new InvalidOperationException("Frame duration factory must return one positive duration per frame.");
        }

        return durations;
    }

    private void ValidateCustomFrames()
    {
        if (Fps <= 0) throw new InvalidOperationException("Animation FPS must be positive.");
        if (SourceFrameIndices is not null &&
            (SourceFrameIndices.Count != FrameCount || SourceFrameIndices.Any(value => value < 0)))
            throw new InvalidOperationException("Source frame indices must match the animation frame count and be non-negative.");
        if (FrameHoldCounts is not null &&
            (FrameHoldCounts.Count != FrameCount || FrameHoldCounts.Any(value => value <= 0)))
            throw new InvalidOperationException("Frame hold counts must match the animation frame count and be positive.");
    }
}
