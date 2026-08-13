namespace GaeulDesktopPet.Models;

public enum InteractionFrequencyLevel
{
    Off = 0,
    Occasional = 1,
    Often = 2,
    Frequent = 3,
    Continuous = 4
}

public sealed class PetSettings
{
    public const double MinimumSizeScale = 0.75;
    public const double MaximumSizeScale = 5.0;

    public double? Left { get; set; }
    public double? Top { get; set; }
    public double SizeScale { get; set; } = 1.0;
    public InteractionFrequencyLevel InteractionFrequency { get; set; } = InteractionFrequencyLevel.Often;
    public bool StartWithWindows { get; set; }
    public bool SuppressInFullscreen { get; set; } = true;
    public bool Hidden { get; set; }
    public string? SelectedInteractionName { get; set; }
    public bool ContinuousActionEnabled { get; set; }

    public void Validate()
    {
        SizeScale = Math.Clamp(SizeScale, MinimumSizeScale, MaximumSizeScale);
        if (!Enum.IsDefined(InteractionFrequency)) InteractionFrequency = InteractionFrequencyLevel.Often;
        if (string.IsNullOrWhiteSpace(SelectedInteractionName))
        {
            SelectedInteractionName = null;
            ContinuousActionEnabled = false;
        }
    }
}
