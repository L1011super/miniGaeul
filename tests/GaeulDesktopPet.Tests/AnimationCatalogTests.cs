using GaeulDesktopPet.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GaeulDesktopPet.Tests;

[TestClass]
public sealed class AnimationCatalogTests
{
    private static string AssetRoot => FindAssetRoot();

    private static string FindAssetRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "src", "GaeulDesktopPet",
                    "Assets", "Sprites", AnimationCatalog.CharacterId);
                if (Directory.Exists(candidate)) return candidate;
            }
        }

        throw new DirectoryNotFoundException($"Could not locate assets for {AnimationCatalog.CharacterId}.");
    }

    [TestMethod]
    public void Catalog_contains_the_complete_single_character_asset_set()
    {
        AnimationCatalog.ValidateAssets(AssetRoot);

        Assert.AreEqual("01_gaeul_kitsch", AnimationCatalog.CharacterId);
        Assert.IsTrue(AnimationCatalog.Idle.Loop);
        Assert.AreEqual(8, AnimationCatalog.Interactions.Count);
        Assert.AreEqual(10, AnimationCatalog.SettingsActions.Count);
        Assert.IsTrue(AnimationCatalog.Interactions.All(animation => !animation.Loop));
        Assert.AreEqual(15, AnimationCatalog.Idle.Fps);
        Assert.IsTrue(AnimationCatalog.All.Where(animation => !ReferenceEquals(animation, AnimationCatalog.Idle))
            .All(animation => animation.Fps == 10));
        Assert.AreEqual(128, AnimationCatalog.All.Sum(animation => animation.FrameCount));
        Assert.AreEqual(16, AnimationCatalog.FindInteraction("interact_angry")?.FrameCount);
        Assert.AreEqual(10, AnimationCatalog.FindInteraction("interact_wink")?.FrameCount);
        Assert.IsNull(AnimationCatalog.FindInteraction("interact_arms_open"));
        CollectionAssert.AreEquivalent(
            new[] { "drag_left.png", "drag_right.png" },
            AnimationCatalog.StaticFrames.ToArray());
        Assert.AreEqual(122, Directory.GetFiles(AssetRoot, "*.png", SearchOption.AllDirectories).Length);
        CollectionAssert.AreEquivalent(
            AnimationCatalog.SettingsActions.Select(action => action.Name).ToArray(),
            AnimationCatalog.RandomActions.Select(action => action.Name).ToArray());
    }

    [TestMethod]
    public void Frame_names_expand_with_three_digits()
    {
        Assert.AreEqual("idle_approved_f000.png", AnimationCatalog.Idle.GetFrameFileName(0));
        Assert.AreEqual("idle_approved_f026.png", AnimationCatalog.Idle.GetFrameFileName(1));
        Assert.AreEqual("idle_approved_f029.png", AnimationCatalog.Idle.GetFrameFileName(4));
        Assert.AreEqual(
            "interact_angry_f015.png",
            AnimationCatalog.FindInteraction("interact_angry")?.GetFrameFileName(15));
    }

    [TestMethod]
    public void Idle_blink_plays_at_fifteen_fps_with_configured_static_holds()
    {
        var random = new Random(20260817);
        for (var cycle = 0; cycle < 50; cycle++)
        {
            var durations = AnimationCatalog.CreateIdleFrameDurations(random);
            Assert.AreEqual(5, durations.Count);
            Assert.IsTrue(durations[0] >= TimeSpan.FromMilliseconds(AnimationCatalog.IdleLeadingStaticMinimumMilliseconds));
            Assert.IsTrue(durations[0] <= TimeSpan.FromMilliseconds(AnimationCatalog.IdleLeadingStaticMaximumMilliseconds));
            for (var frame = 1; frame <= 3; frame++)
            {
                Assert.AreEqual(AnimationCatalog.IdleBlinkFrameDuration, durations[frame]);
            }
            Assert.AreEqual(AnimationCatalog.IdleTrailingStaticDuration, durations[4]);
        }
    }

    [TestMethod]
    public void Move_right_plays_two_direct_walk_cycles()
    {
        Assert.AreEqual(16, AnimationCatalog.MoveRight.FrameCount);
        Assert.IsFalse(AnimationCatalog.MoveRight.Loop);
        Assert.AreEqual(TimeSpan.FromMilliseconds(100), AnimationCatalog.MoveRight.GetFrameDuration(0));
        Assert.AreEqual(
            Path.Combine("01_gaeul_move", "move_right_f000.png"),
            AnimationCatalog.MoveRight.GetFrameRelativePath(0));
        Assert.AreEqual(
            Path.Combine("01_gaeul_move", "move_right_f007.png"),
            AnimationCatalog.MoveRight.GetFrameRelativePath(7));
        Assert.AreEqual(
            AnimationCatalog.MoveRight.GetFrameRelativePath(0),
            AnimationCatalog.MoveRight.GetFrameRelativePath(8));
        Assert.AreEqual(
            AnimationCatalog.MoveRight.GetFrameRelativePath(7),
            AnimationCatalog.MoveRight.GetFrameRelativePath(15));
        CollectionAssert.AreEqual(
            Enumerable.Range(0, 16).ToArray(),
            Enumerable.Range(0, AnimationCatalog.MoveRight.FrameCount)
                .Where(AnimationCatalog.IsMoveRightTravelFrame)
                .ToArray());
        Assert.AreSame(
            AnimationCatalog.MoveRight,
            AnimationCatalog.FindSettingsAction("MOVE_RIGHT"));
        Assert.IsNull(AnimationCatalog.FindInteraction("move_right"));
    }

    [TestMethod]
    public void Move_left_plays_eight_direct_travel_frames_without_transition()
    {
        Assert.AreEqual(8, AnimationCatalog.MoveLeft.FrameCount);
        Assert.IsFalse(AnimationCatalog.MoveLeft.Loop);
        Assert.AreEqual(
            Path.Combine("01_gaeul_mov2", "move_left_f000.png"),
            AnimationCatalog.MoveLeft.GetFrameRelativePath(0));
        Assert.AreEqual(
            Path.Combine("01_gaeul_mov2", "move_left_f007.png"),
            AnimationCatalog.MoveLeft.GetFrameRelativePath(7));
        CollectionAssert.AreEqual(
            Enumerable.Range(0, 8).ToArray(),
            Enumerable.Range(0, AnimationCatalog.MoveLeft.FrameCount)
                .Where(AnimationCatalog.IsMoveLeftTravelFrame)
                .ToArray());
        Assert.AreSame(
            AnimationCatalog.MoveLeft,
            AnimationCatalog.FindSettingsAction("MOVE_LEFT"));
    }

    [TestMethod]
    public void Random_walks_use_one_through_four_complete_step_cycles()
    {
        for (var cycles = AnimationCatalog.RandomWalkMinimumCycles;
             cycles <= AnimationCatalog.RandomWalkMaximumCycles;
             cycles++)
        {
            var right = AnimationCatalog.CreateRandomMoveRight(cycles);
            var left = AnimationCatalog.CreateRandomMoveLeft(cycles);

            Assert.AreEqual(AnimationCatalog.MoveRightWalkFrameCount * cycles, right.FrameCount);
            Assert.AreEqual(AnimationCatalog.MoveLeftWalkFrameCount * cycles, left.FrameCount);
            if (cycles > 1)
            {
                Assert.AreEqual(right.GetFrameRelativePath(0), right.GetFrameRelativePath(AnimationCatalog.MoveRightWalkFrameCount));
                Assert.AreEqual(left.GetFrameRelativePath(0), left.GetFrameRelativePath(AnimationCatalog.MoveLeftWalkFrameCount));
            }
        }

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => AnimationCatalog.CreateRandomMoveRight(0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => AnimationCatalog.CreateRandomMoveLeft(5));
    }

    [TestMethod]
    public void Angry_inserted_frames_repeat_five_through_seven_at_eight_through_ten()
    {
        for (var sourceIndex = 5; sourceIndex <= 7; sourceIndex++)
        {
            var insertedIndex = sourceIndex + 3;
            CollectionAssert.AreEqual(
                File.ReadAllBytes(Path.Combine(AssetRoot, $"interact_angry_f{sourceIndex:D3}.png")),
                File.ReadAllBytes(Path.Combine(AssetRoot, $"interact_angry_f{insertedIndex:D3}.png")));
        }
    }

    [TestMethod]
    public void Wink_repeats_the_hold_frame_four_times_before_returning_to_idle()
    {
        var wink = AnimationCatalog.FindInteraction("interact_wink");
        Assert.IsNotNull(wink);
        Assert.AreEqual(10, wink.FrameCount);
        Assert.AreEqual("interact_wink_f000.png", wink.GetFrameFileName(0));
        Assert.AreEqual("interact_wink_f009.png", wink.GetFrameFileName(9));

        for (var index = 0; index < wink.FrameCount; index++)
        {
            Assert.IsTrue(File.Exists(Path.Combine(AssetRoot, wink.GetFrameFileName(index))));
        }

        var holdFrame = File.ReadAllBytes(Path.Combine(AssetRoot, "interact_wink_f003.png"));
        for (var index = 4; index <= 6; index++)
        {
            CollectionAssert.AreEqual(holdFrame,
                File.ReadAllBytes(Path.Combine(AssetRoot, $"interact_wink_f{index:D3}.png")));
        }

    }

    [TestMethod]
    public void Interaction_lookup_excludes_idle_and_resolves_known_actions()
    {
        Assert.IsNull(AnimationCatalog.FindInteraction(AnimationCatalog.Idle.Name));
        Assert.IsNull(AnimationCatalog.FindInteraction(null));
        Assert.AreEqual(
            "interact_wave",
            AnimationCatalog.FindInteraction("INTERACT_WAVE")?.Name);
        Assert.AreSame(
            AnimationCatalog.FindInteraction("interact_wink"),
            AnimationCatalog.FindSettingsAction("interact_wink"));
        Assert.IsNull(AnimationCatalog.FindSettingsAction("interact_arms_open"));
    }

    [TestMethod]
    public void Indexed_frame_uses_palette_alpha_for_hit_testing()
    {
        var cache = new AnimationFrameCache();
        var bitmap = cache.Get(Path.Combine(AssetRoot, "idle_approved_f000.png"));

        Assert.AreEqual("Indexed8", bitmap.Format.ToString());
        Assert.AreEqual(0, cache.GetAlpha(bitmap, 0, 0));
        Assert.IsTrue(cache.GetAlpha(bitmap, 300, 300) >= 15);
        var bodyBounds = cache.GetOpaqueBounds(bitmap);
        Assert.AreEqual(241, bodyBounds.Left, 0.001);
        Assert.AreEqual(200, bodyBounds.Top, 0.001);
        Assert.AreEqual(114, bodyBounds.Width, 0.001);
        Assert.AreEqual(300, bodyBounds.Height, 0.001);
    }

    [TestMethod]
    public void Drag_frames_are_decodable_static_assets()
    {
        var cache = new AnimationFrameCache();
        foreach (var fileName in AnimationCatalog.StaticFrames)
        {
            var bitmap = cache.Get(Path.Combine(AssetRoot, fileName));
            Assert.IsTrue(bitmap.PixelWidth > 0);
            Assert.IsTrue(bitmap.PixelHeight > 0);
        }
    }
}
