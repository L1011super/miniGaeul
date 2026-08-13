using GaeulDesktopPet.Models;

namespace GaeulDesktopPet.Services;

public static class AnimationCatalog
{
    public const string CharacterId = "01_gaeul_kitsch";
    public const string DragLeftFrame = "drag_left.png";
    public const string DragRightFrame = "drag_right.png";
    public const string MoveAssetDirectory = "01_gaeul_move";
    public const int MoveRightTransitionFrameCount = 3;
    public const int MoveRightWalkFrameCount = 13;
    public const int MoveRightWalkCycles = 2;

    public static AnimationDefinition Idle { get; } = new(
        "idle_approved",
        true,
        10,
        5,
        [0, 26, 27, 28, 29],
        [26, 1, 1, 1, 1]);

    public static AnimationDefinition MoveRight { get; } = new(
        "move_right",
        false,
        10,
        MoveRightTransitionFrameCount * 2 + MoveRightWalkFrameCount * MoveRightWalkCycles,
        Enumerable.Range(13, MoveRightTransitionFrameCount)
            .Concat(Enumerable.Range(0, MoveRightWalkFrameCount))
            .Concat(Enumerable.Range(0, MoveRightWalkFrameCount))
            .Concat(Enumerable.Range(16, MoveRightTransitionFrameCount))
            .ToArray(),
        AssetDirectory: MoveAssetDirectory);

    public static bool IsMoveRightTravelFrame(int frameIndex) =>
        frameIndex >= MoveRightTransitionFrameCount &&
        frameIndex < MoveRightTransitionFrameCount + MoveRightWalkFrameCount * MoveRightWalkCycles;

    public static IReadOnlyList<AnimationDefinition> Interactions { get; } =
    [
        new("interact_think_question", false, 10, 19),
        new("interact_angry", false, 10, 16),
        new("interact_wave", false, 10, 11),
        new("interact_happy_jump", false, 10, 9),
        new("interact_hands_clasp", false, 10, 11),
        new("interact_arms_open", false, 10, 12),
        new("interact_clasp_sway", false, 10, 11),
        new("interact_arms_crossed", false, 10, 12)
    ];

    public static IReadOnlyList<AnimationDefinition> SettingsActions { get; } =
        Interactions.Append(MoveRight).ToArray();

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
