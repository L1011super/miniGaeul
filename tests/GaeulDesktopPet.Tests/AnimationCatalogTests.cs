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
        Assert.AreEqual(9, AnimationCatalog.SettingsActions.Count);
        Assert.IsTrue(AnimationCatalog.Interactions.All(animation => !animation.Loop));
        Assert.IsTrue(AnimationCatalog.All.All(animation => animation.Fps == 10));
        Assert.AreEqual(138, AnimationCatalog.All.Sum(animation => animation.FrameCount));
        Assert.AreEqual(16, AnimationCatalog.FindInteraction("interact_angry")?.FrameCount);
        CollectionAssert.AreEquivalent(
            new[] { "drag_left.png", "drag_right.png" },
            AnimationCatalog.StaticFrames.ToArray());
        Assert.AreEqual(127, Directory.GetFiles(AssetRoot, "*.png", SearchOption.AllDirectories).Length);
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
    public void Idle_static_frame_preserves_the_original_26_frame_hold()
    {
        Assert.AreEqual(TimeSpan.FromMilliseconds(2600), AnimationCatalog.Idle.GetFrameDuration(0));
        for (var index = 1; index < AnimationCatalog.Idle.FrameCount; index++)
        {
            Assert.AreEqual(TimeSpan.FromMilliseconds(100), AnimationCatalog.Idle.GetFrameDuration(index));
        }
    }

    [TestMethod]
    public void Move_right_wraps_two_walk_cycles_with_idle_transitions()
    {
        Assert.AreEqual(32, AnimationCatalog.MoveRight.FrameCount);
        Assert.IsFalse(AnimationCatalog.MoveRight.Loop);
        Assert.AreEqual(TimeSpan.FromMilliseconds(100), AnimationCatalog.MoveRight.GetFrameDuration(0));
        Assert.AreEqual(
            Path.Combine("01_gaeul_move", "move_right_f013.png"),
            AnimationCatalog.MoveRight.GetFrameRelativePath(0));
        Assert.AreEqual(
            Path.Combine("01_gaeul_move", "move_right_f000.png"),
            AnimationCatalog.MoveRight.GetFrameRelativePath(3));
        Assert.AreEqual(
            AnimationCatalog.MoveRight.GetFrameRelativePath(3),
            AnimationCatalog.MoveRight.GetFrameRelativePath(16));
        Assert.AreEqual(
            Path.Combine("01_gaeul_move", "move_right_f018.png"),
            AnimationCatalog.MoveRight.GetFrameRelativePath(31));
        CollectionAssert.AreEqual(
            Enumerable.Range(3, 26).ToArray(),
            Enumerable.Range(0, AnimationCatalog.MoveRight.FrameCount)
                .Where(AnimationCatalog.IsMoveRightTravelFrame)
                .ToArray());
        Assert.AreSame(
            AnimationCatalog.MoveRight,
            AnimationCatalog.FindSettingsAction("MOVE_RIGHT"));
        Assert.IsNull(AnimationCatalog.FindInteraction("move_right"));
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
    public void Interaction_lookup_excludes_idle_and_resolves_known_actions()
    {
        Assert.IsNull(AnimationCatalog.FindInteraction(AnimationCatalog.Idle.Name));
        Assert.IsNull(AnimationCatalog.FindInteraction(null));
        Assert.AreEqual(
            "interact_wave",
            AnimationCatalog.FindInteraction("INTERACT_WAVE")?.Name);
    }

    [TestMethod]
    public void Indexed_frame_uses_palette_alpha_for_hit_testing()
    {
        var cache = new AnimationFrameCache();
        var bitmap = cache.Get(Path.Combine(AssetRoot, "idle_approved_f000.png"));

        Assert.AreEqual("Indexed8", bitmap.Format.ToString());
        Assert.AreEqual(0, cache.GetAlpha(bitmap, 0, 0));
        Assert.IsTrue(cache.GetAlpha(bitmap, 300, 300) >= 15);
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
