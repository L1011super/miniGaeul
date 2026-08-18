using GaeulDesktopPet.Models;

namespace GaeulDesktopPet.Services;

public static class AnimationCatalog
{
    public const string CharacterId = "01_gaeul_kitsch";
    public const string DragLeftFrame = "drag_left.png";
    public const string DragRightFrame = "drag_right.png";
    public const string MoveAssetDirectory = "01_gaeul_move";
    public const string MoveLeftAssetDirectory = "01_gaeul_mov2";
    public const int MoveRightWalkFrameCount = 8;
    public const int MoveRightWalkCycles = 2;
    public const int MoveLeftWalkFrameCount = 8;
    public const int RandomWalkMinimumCycles = 1;
    public const int RandomWalkMaximumCycles = 4;
    public const int IdleFramesPerSecond = 15;
    public const int IdleLeadingStaticMinimumMilliseconds = 300;
    public const int IdleLeadingStaticMaximumMilliseconds = 1500;
    public static readonly TimeSpan IdleBlinkFrameDuration = TimeSpan.FromSeconds(1.0 / IdleFramesPerSecond);
    public static readonly TimeSpan IdleTrailingStaticDuration = TimeSpan.FromSeconds(2);

    public static AnimationDefinition Idle { get; } = new(
        "idle_approved",
        true,
        IdleFramesPerSecond,
        5,
        [0, 26, 27, 28, 29],
        [5, 1, 1, 1, 15],
        FrameDurationFactory: () => CreateIdleFrameDurations());

    public static IReadOnlyList<TimeSpan> CreateIdleFrameDurations(Random? random = null)
    {
        var generator = random ?? Random.Shared;
        var beforeBlinkMilliseconds = generator.Next(
            IdleLeadingStaticMinimumMilliseconds,
            IdleLeadingStaticMaximumMilliseconds + 1);
        return
        [
            TimeSpan.FromMilliseconds(beforeBlinkMilliseconds),
            IdleBlinkFrameDuration,
            IdleBlinkFrameDuration,
            IdleBlinkFrameDuration,
            IdleTrailingStaticDuration
        ];
    }

    public static AnimationDefinition MoveRight { get; } = new(
        "move_right",
        false,
        10,
        MoveRightWalkFrameCount * MoveRightWalkCycles,
        Enumerable.Range(0, MoveRightWalkFrameCount)
            .Concat(Enumerable.Range(0, MoveRightWalkFrameCount))
            .ToArray(),
        AssetDirectory: MoveAssetDirectory);

    public static AnimationDefinition MoveLeft { get; } = new(
        "move_left",
        false,
        10,
        MoveLeftWalkFrameCount,
        AssetDirectory: MoveLeftAssetDirectory);

    public static AnimationDefinition CreateRandomMoveRight(int cycles) =>
        CreateMoveAnimation("move_right", MoveAssetDirectory, MoveRightWalkFrameCount, cycles);

    public static AnimationDefinition CreateRandomMoveLeft(int cycles) =>
        CreateMoveAnimation("move_left", MoveLeftAssetDirectory, MoveLeftWalkFrameCount, cycles);

    public static bool IsMoveRightTravelFrame(int frameIndex) =>
        (uint)frameIndex < (uint)MoveRight.FrameCount;

    public static bool IsMoveLeftTravelFrame(int frameIndex) =>
        (uint)frameIndex < (uint)MoveLeftWalkFrameCount;

    public static IReadOnlyList<AnimationDefinition> Interactions { get; } =
    [
        new("interact_think_question", false, 10, 19),
        new("interact_angry", false, 10, 16),
        new("interact_wave", false, 10, 11),
        new("interact_happy_jump", false, 10, 9),
        new("interact_hands_clasp", false, 10, 11),
        new("interact_wink", false, 10, 10),
        new("interact_clasp_sway", false, 10, 11),
        new("interact_arms_crossed", false, 10, 12)
    ];

    public static IReadOnlyList<AnimationDefinition> SettingsActions { get; } =
        Interactions.Append(MoveRight).Append(MoveLeft).ToArray();

    public static IReadOnlyList<AnimationDefinition> RandomActions => SettingsActions;

    public static IEnumerable<AnimationDefinition> All => SettingsActions.Prepend(Idle);

    public static IReadOnlyList<string> StaticFrames { get; } =
    [
        DragLeftFrame,
        DragRightFrame
    ];

    public static AnimationDefinition? FindInteraction(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : Interactions.FirstOrDefault(animation =>
                string.Equals(animation.Name, name, StringComparison.OrdinalIgnoreCase));

    public static AnimationDefinition? FindSettingsAction(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : SettingsActions.FirstOrDefault(animation =>
                string.Equals(animation.Name, name, StringComparison.OrdinalIgnoreCase));

    private static AnimationDefinition CreateMoveAnimation(
        string name,
        string assetDirectory,
        int walkFrameCount,
        int cycles)
    {
        if (cycles is < RandomWalkMinimumCycles or > RandomWalkMaximumCycles)
        {
            throw new ArgumentOutOfRangeException(nameof(cycles));
        }

        return new AnimationDefinition(
            name,
            false,
            10,
            walkFrameCount * cycles,
            Enumerable.Range(0, cycles)
                .SelectMany(_ => Enumerable.Range(0, walkFrameCount))
                .ToArray(),
            AssetDirectory: assetDirectory);
    }

    public static string ResolveAssetRoot()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", "Sprites", CharacterId),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", "Sprites", CharacterId))
        };

        return candidates.FirstOrDefault(Directory.Exists)
            ?? throw new DirectoryNotFoundException($"Animation assets not found. Expected: {candidates[0]}");
    }

    public static void ValidateAssets(string assetRoot)
    {
        var expected = All
            .SelectMany(animation => Enumerable.Range(0, animation.FrameCount)
                .Select(animation.GetFrameRelativePath))
            .Concat(StaticFrames)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var actual = Directory.GetFiles(assetRoot, "*.png", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(assetRoot, path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = expected.Except(actual).ToArray();
        var unexpected = actual.Except(expected).ToArray();
        if (missing.Length > 0 || unexpected.Length > 0)
        {
            throw new InvalidOperationException(
                $"Animation asset set is invalid. Missing: {string.Join(", ", missing)}; unexpected: {string.Join(", ", unexpected)}");
        }

        LogService.Info($"Animation assets validated: {All.Count()} animations, {expected.Count} frames");
    }
}
