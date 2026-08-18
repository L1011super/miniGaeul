using GaeulDesktopPet.Models;
using GaeulDesktopPet.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WpfRect = System.Windows.Rect;
using WpfSize = System.Windows.Size;

namespace GaeulDesktopPet.Tests;

[TestClass]
public sealed class SchedulerAndSettingsTests
{
    [TestMethod]
    public void Interaction_frequency_maps_to_the_requested_fixed_delays()
    {
        var scheduler = new RandomActionScheduler();
        var expected = new Dictionary<InteractionFrequencyLevel, TimeSpan?>
        {
            [InteractionFrequencyLevel.Off] = null,
            [InteractionFrequencyLevel.Occasional] = TimeSpan.FromMinutes(20),
            [InteractionFrequencyLevel.Often] = TimeSpan.FromMinutes(5),
            [InteractionFrequencyLevel.Frequent] = TimeSpan.FromMinutes(1),
            [InteractionFrequencyLevel.Continuous] = TimeSpan.FromSeconds(3)
        };

        foreach (var (frequency, delay) in expected)
        {
            var settings = new PetSettings { InteractionFrequency = frequency };
            Assert.AreEqual(delay, scheduler.NextDelay(settings));
        }
    }

    [TestMethod]
    public void Recent_picker_avoids_recent_two_when_possible()
    {
        var picker = new RecentActionPicker();
        var actions = new[]
        {
            new AnimationDefinition("a", false, 10, 1),
            new AnimationDefinition("b", false, 10, 1),
            new AnimationDefinition("c", false, 10, 1),
            new AnimationDefinition("d", false, 10, 1)
        };
        var first = picker.Pick(actions);
        var second = picker.Pick(actions);
        var third = picker.Pick(actions);
        Assert.AreNotEqual(first.Name, third.Name);
        Assert.AreNotEqual(second.Name, third.Name);
    }

    [TestMethod]
    public void Settings_validation_recovers_invalid_values()
    {
        var settings = new PetSettings
        {
            SizeScale = 9,
            InteractionFrequency = (InteractionFrequencyLevel)99,
            ContinuousActionEnabled = true
        };
        settings.Validate();
        Assert.AreEqual(PetSettings.MaximumSizeScale, settings.SizeScale);
        Assert.AreEqual(2.5, PetSettings.MaximumSizeScale, 0.001);
        Assert.AreEqual(1.75, PetSettings.DefaultSizeScale, 0.001);
        Assert.AreEqual(InteractionFrequencyLevel.Often, settings.InteractionFrequency);
        Assert.IsFalse(settings.ContinuousActionEnabled);
    }

    [TestMethod]
    public void Settings_panel_moves_to_left_when_right_side_is_outside_work_area()
    {
        var position = ScreenService.CalculateAdjacentPosition(
            new WpfRect(1800, 200, 100, 100),
            new WpfRect(0, 0, 1920, 1080),
            new WpfSize(340, 430),
            100);

        Assert.AreEqual(1360, position.X);
        Assert.AreEqual(200, position.Y);
    }

    [TestMethod]
    public void Settings_panel_defaults_to_right_with_requested_gap()
    {
        var position = ScreenService.CalculateAdjacentPosition(
            new WpfRect(500, 200, 100, 100),
            new WpfRect(0, 0, 1920, 1080),
            new WpfSize(340, 430),
            100);

        Assert.AreEqual(700, position.X);
        Assert.AreEqual(200, position.Y);
    }

    [TestMethod]
    public void Settings_panel_stays_on_right_when_the_panel_fits_completely()
    {
        var position = ScreenService.CalculateAdjacentPosition(
            new WpfRect(1300, 200, 100, 100),
            new WpfRect(0, 0, 1920, 1080),
            new WpfSize(340, 430),
            100);

        Assert.AreEqual(1500, position.X);
        Assert.AreEqual(200, position.Y);
    }

    [TestMethod]
    public void Settings_panel_uses_right_side_when_space_equals_threshold()
    {
        var position = ScreenService.CalculateAdjacentPosition(
            new WpfRect(1380, 200, 100, 100),
            new WpfRect(0, 0, 1920, 1080),
            new WpfSize(340, 430),
            100);

        Assert.AreEqual(1580, position.X);
        Assert.AreEqual(200, position.Y);
    }

    [TestMethod]
    public void Settings_panel_preserves_height_and_gap_when_anchor_moves()
    {
        var workArea = new WpfRect(0, 0, 1920, 1080);
        var targetSize = new WpfSize(340, 430);
        var first = ScreenService.CalculateAdjacentPosition(
            new WpfRect(400, 200, 100, 100),
            workArea,
            targetSize,
            100);
        var moved = ScreenService.CalculateAdjacentPosition(
            new WpfRect(650, 350, 100, 100),
            workArea,
            targetSize,
            100);

        Assert.AreEqual(600, first.X);
        Assert.AreEqual(200, first.Y);
        Assert.AreEqual(850, moved.X);
        Assert.AreEqual(350, moved.Y);
    }

    [TestMethod]
    public void Settings_panel_switches_sides_when_movement_crosses_the_threshold()
    {
        var workArea = new WpfRect(0, 0, 1920, 1080);
        var targetSize = new WpfSize(340, 430);
        var atThreshold = ScreenService.CalculateAdjacentPosition(
            new WpfRect(1380, 200, 100, 100),
            workArea,
            targetSize,
            100);
        var pastThreshold = ScreenService.CalculateAdjacentPosition(
            new WpfRect(1381, 350, 100, 100),
            workArea,
            targetSize,
            100);

        Assert.AreEqual(1580, atThreshold.X);
        Assert.AreEqual(200, atThreshold.Y);
        Assert.AreEqual(941, pastThreshold.X);
        Assert.AreEqual(350, pastThreshold.Y);
    }

    [TestMethod]
    public void Settings_panel_is_forced_right_and_clamped_to_bottom_when_anchor_is_inside_the_left_threshold()
    {
        var position = ScreenService.CalculateAdjacentPosition(
            new WpfRect(0, 1000, 100, 100),
            new WpfRect(0, 0, 500, 1080),
            new WpfSize(340, 430),
            100);

        Assert.AreEqual(200, position.X);
        Assert.AreEqual(650, position.Y);
    }

    [TestMethod]
    public void Settings_panel_can_use_left_side_when_left_space_equals_threshold()
    {
        var position = ScreenService.CalculateAdjacentPosition(
            new WpfRect(440, 240, 100, 100),
            new WpfRect(0, 0, 900, 1080),
            new WpfSize(340, 430),
            100);

        Assert.AreEqual(0, position.X);
        Assert.AreEqual(240, position.Y);
    }

    [TestMethod]
    public void Settings_panel_switches_right_just_inside_the_left_threshold()
    {
        var position = ScreenService.CalculateAdjacentPosition(
            new WpfRect(439, 360, 100, 100),
            new WpfRect(0, 0, 900, 1080),
            new WpfSize(340, 430),
            100);

        Assert.AreEqual(639, position.X);
        Assert.AreEqual(360, position.Y);
    }

    [TestMethod]
    public void Settings_panel_uses_monitor_relative_left_threshold_on_negative_coordinates()
    {
        var position = ScreenService.CalculateAdjacentPosition(
            new WpfRect(-1480, 180, 100, 100),
            new WpfRect(-1920, 0, 900, 1080),
            new WpfSize(340, 430),
            100);

        Assert.AreEqual(-1920, position.X);
        Assert.AreEqual(180, position.Y);
    }

    [TestMethod]
    public void Settings_panel_initial_top_is_clamped_to_work_area_top()
    {
        var position = ScreenService.CalculateAdjacentPosition(
            new WpfRect(500, -120, 100, 100),
            new WpfRect(0, 0, 1920, 1080),
            new WpfSize(340, 430),
            100);

        Assert.AreEqual(700, position.X);
        Assert.AreEqual(0, position.Y);
    }

    [TestMethod]
    public void Settings_panel_keeps_the_anchor_top_when_it_exactly_fits_above_the_bottom_edge()
    {
        var position = ScreenService.CalculateAdjacentPosition(
            new WpfRect(500, 650, 100, 100),
            new WpfRect(0, 0, 1920, 1080),
            new WpfSize(340, 430),
            100);

        Assert.AreEqual(700, position.X);
        Assert.AreEqual(650, position.Y);
    }

    [TestMethod]
    public void Settings_panel_keeps_the_anchor_top_when_there_is_space_on_both_vertical_sides()
    {
        var position = ScreenService.CalculateAdjacentPosition(
            new WpfRect(500, 325, 100, 100),
            new WpfRect(0, 0, 1920, 1080),
            new WpfSize(340, 430),
            100);

        Assert.AreEqual(700, position.X);
        Assert.AreEqual(325, position.Y);
    }

    [TestMethod]
    public void Settings_panel_initial_bottom_is_clamped_on_negative_vertical_coordinates()
    {
        var position = ScreenService.CalculateAdjacentPosition(
            new WpfRect(-1400, -100, 100, 100),
            new WpfRect(-1920, -1080, 1920, 1080),
            new WpfSize(340, 430),
            100);

        Assert.AreEqual(-1200, position.X);
        Assert.AreEqual(-430, position.Y);
    }

    [TestMethod]
    public void Diagonal_walk_advances_by_both_requested_distances_when_space_is_available()
    {
        var position = ScreenService.CalculateMovementPosition(
            new WpfRect(500, 200, 320, 320),
            new WpfRect(0, 0, 1920, 1080),
            7.62,
            -4.4,
            12);

        Assert.AreEqual(507.62, position.X, 0.001);
        Assert.AreEqual(195.6, position.Y, 0.001);
    }

    [TestMethod]
    public void Right_walk_stops_before_the_screen_edge_margin()
    {
        var position = ScreenService.CalculateMovementPosition(
            new WpfRect(1585, 200, 320, 320),
            new WpfRect(0, 0, 1920, 1080),
            8,
            4,
            12);

        Assert.AreEqual(1588, position.X);
        Assert.AreEqual(204, position.Y);
    }

    [TestMethod]
    public void Right_walk_never_moves_farther_right_when_already_at_the_boundary()
    {
        var position = ScreenService.CalculateMovementPosition(
            new WpfRect(1588, 200, 320, 320),
            new WpfRect(0, 0, 1920, 1080),
            8,
            0,
            12);

        Assert.AreEqual(1588, position.X);
    }

    [TestMethod]
    public void Diagonal_walk_stays_inside_top_and_bottom_edge_margins()
    {
        var upward = ScreenService.CalculateMovementPosition(
            new WpfRect(500, 13, 320, 320),
            new WpfRect(0, 0, 1920, 1080),
            8,
            -4,
            12);
        var downward = ScreenService.CalculateMovementPosition(
            new WpfRect(500, 747, 320, 320),
            new WpfRect(0, 0, 1920, 1080),
            8,
            4,
            12);

        Assert.AreEqual(12, upward.Y);
        Assert.AreEqual(748, downward.Y);
    }

    [TestMethod]
    public void Idle_character_bounds_scale_with_the_window_size()
    {
        var bounds = ScreenService.ScaleSpriteBounds(
            new WpfRect(241, 200, 114, 300),
            new WpfSize(600, 600),
            new WpfSize(300, 300));

        Assert.AreEqual(120.5, bounds.Left, 0.001);
        Assert.AreEqual(100, bounds.Top, 0.001);
        Assert.AreEqual(57, bounds.Width, 0.001);
        Assert.AreEqual(150, bounds.Height, 0.001);
    }

    [TestMethod]
    public void Scaling_keeps_the_character_center_and_canvas_bottom_anchor_fixed()
    {
        var spriteBounds = new WpfRect(241, 200, 114, 300);
        var spriteSize = new WpfSize(600, 600);
        var originalWindow = new WpfRect(100, 200, 300, 300);
        var anchor = ScreenService.CalculateCharacterAnchor(originalWindow, spriteBounds, spriteSize);
        var resizedPosition = ScreenService.CalculateWindowPositionForCharacterAnchor(
            anchor,
            spriteBounds,
            spriteSize,
            new WpfSize(600, 600));
        var resizedWindow = new WpfRect(resizedPosition, new WpfSize(600, 600));

        var resizedAnchor = ScreenService.CalculateCharacterAnchor(resizedWindow, spriteBounds, spriteSize);
        Assert.AreEqual(anchor.X, resizedAnchor.X, 0.001);
        Assert.AreEqual(anchor.Y, resizedAnchor.Y, 0.001);
        Assert.AreEqual(500, anchor.Y, 0.001);
    }

    [TestMethod]
    public void Walk_boundary_uses_character_bounds_instead_of_transparent_window_padding()
    {
        var position = ScreenService.CalculateMovementPosition(
            new WpfRect(1600, 200, 300, 300),
            new WpfRect(120.5, 100, 57, 150),
            new WpfRect(0, 0, 1920, 1080),
            100,
            0,
            12);

        Assert.AreEqual(1700, position.X, 0.001);
        Assert.AreEqual(200, position.Y, 0.001);
    }

    [TestMethod]
    public void Edge_positions_force_the_next_walk_toward_the_screen_interior()
    {
        var workArea = new WpfRect(0, 0, 1920, 1080);
        var characterBounds = new WpfRect(120.5, 100, 57, 150);
        var leftDirection = ScreenService.GetInwardWalkDirection(
            new WpfRect(-108.5, 200, 300, 300),
            characterBounds,
            workArea,
            12);
        var rightDirection = ScreenService.GetInwardWalkDirection(
            new WpfRect(1850.5, 200, 300, 300),
            characterBounds,
            workArea,
            12);

        Assert.AreEqual(1, leftDirection);
        Assert.AreEqual(-1, rightDirection);
        Assert.IsTrue(ScreenService.HasReachedWorkAreaEdge(
            new WpfRect(1850.5, 200, 300, 300),
            characterBounds,
            workArea,
            12));
    }
}
