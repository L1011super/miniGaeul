using System.Windows;
using GaeulDesktopPet.Models;
using GaeulDesktopPet.Services;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace GaeulDesktopPet.Views;

public enum SettingsChangeKind
{
    Interaction,
    Size,
    Startup,
    Fullscreen,
    Visibility,
    Position
}

public sealed class SettingsChangedEventArgs(SettingsChangeKind kind) : EventArgs
{
    public SettingsChangeKind Kind { get; } = kind;
}

public sealed class InteractionPlaybackRequestedEventArgs(
    AnimationDefinition animation,
    bool continuous) : EventArgs
{
    public AnimationDefinition Animation { get; } = animation;
    public bool Continuous { get; } = continuous;
}

public partial class SettingsWindow : Window
{
    private readonly PetSettings _settings;

    public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;
    public event EventHandler<InteractionPlaybackRequestedEventArgs>? InteractionPlaybackRequested;
    public event EventHandler? ExitRequested;

    public SettingsWindow(PetSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        var interactionOptions = AnimationCatalog.SettingsActions
            .Select(animation => new InteractionOption(GetInteractionDisplayName(animation.Name), animation))
            .ToArray();
        InteractionComboBox.ItemsSource = interactionOptions;
        InteractionComboBox.DisplayMemberPath = nameof(InteractionOption.DisplayName);
        var selectedOption = interactionOptions.FirstOrDefault(option =>
            string.Equals(
                option.Animation.Name,
                settings.SelectedInteractionName,
                StringComparison.OrdinalIgnoreCase));
        InteractionComboBox.SelectedItem = selectedOption;
        ContinuousActionBox.IsEnabled = selectedOption is not null;
        ContinuousActionBox.IsChecked = selectedOption is not null && settings.ContinuousActionEnabled;
        if (selectedOption is null)
        {
            settings.SelectedInteractionName = null;
            settings.ContinuousActionEnabled = false;
        }
        FrequencySlider.Value = (int)settings.InteractionFrequency;
        ScaleSlider.Value = settings.SizeScale;
        StartupBox.IsChecked = settings.StartWithWindows;
        FullscreenBox.IsChecked = settings.SuppressInFullscreen;
        HiddenBox.IsChecked = settings.Hidden;
        WireLiveUpdates();
    }

    private void WireLiveUpdates()
    {
        FrequencySlider.ValueChanged += OnFrequencyChanged;
        StartupBox.Checked += OnToggleChanged;
        StartupBox.Unchecked += OnToggleChanged;
        FullscreenBox.Checked += OnToggleChanged;
        FullscreenBox.Unchecked += OnToggleChanged;
        HiddenBox.Checked += OnToggleChanged;
        HiddenBox.Unchecked += OnToggleChanged;
        ScaleSlider.ValueChanged += OnScaleChanged;
        InteractionComboBox.SelectionChanged += OnInteractionSelectionChanged;
        ContinuousActionBox.Checked += OnContinuousActionChanged;
        ContinuousActionBox.Unchecked += OnContinuousActionChanged;
    }

    private void OnInteractionSelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        ContinuousActionBox.IsEnabled = SelectedInteraction is not null;
        _settings.SelectedInteractionName = SelectedInteraction?.Name;
        if (SelectedInteraction is null)
        {
            _settings.ContinuousActionEnabled = false;
            ContinuousActionBox.IsChecked = false;
        }
        RaiseInteractionPlaybackRequest();
    }

    private void OnContinuousActionChanged(object sender, RoutedEventArgs e)
    {
        _settings.ContinuousActionEnabled =
            SelectedInteraction is not null && ContinuousActionBox.IsChecked == true;
        RaiseInteractionPlaybackRequest();
    }

    private void RaiseInteractionPlaybackRequest()
    {
        if (SelectedInteraction is not { } animation) return;
        InteractionPlaybackRequested?.Invoke(
            this,
            new InteractionPlaybackRequestedEventArgs(
                animation,
                _settings.ContinuousActionEnabled));
    }

    public AnimationDefinition? SelectedInteraction =>
        (InteractionComboBox.SelectedItem as InteractionOption)?.Animation;

    public bool ContinuousActionEnabled => ContinuousActionBox.IsChecked == true;

    private void OnToggleChanged(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, StartupBox))
        {
            var value = StartupBox.IsChecked == true;
            if (_settings.StartWithWindows == value) return;
            _settings.StartWithWindows = value;
            RaiseSettingsChanged(SettingsChangeKind.Startup);
        }
        else if (ReferenceEquals(sender, FullscreenBox))
        {
            var value = FullscreenBox.IsChecked == true;
            if (_settings.SuppressInFullscreen == value) return;
            _settings.SuppressInFullscreen = value;
            RaiseSettingsChanged(SettingsChangeKind.Fullscreen);
        }
        else if (ReferenceEquals(sender, HiddenBox))
        {
            var value = HiddenBox.IsChecked == true;
            if (_settings.Hidden == value) return;
            _settings.Hidden = value;
            RaiseSettingsChanged(SettingsChangeKind.Visibility);
        }
    }

    private void OnFrequencyChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var frequency = (InteractionFrequencyLevel)Math.Clamp((int)Math.Round(e.NewValue), 0, 4);
        if (_settings.InteractionFrequency == frequency) return;
        _settings.InteractionFrequency = frequency;
        RaiseSettingsChanged(SettingsChangeKind.Interaction);
    }

    private void OnScaleChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Math.Abs(_settings.SizeScale - e.NewValue) < 0.001) return;
        _settings.SizeScale = e.NewValue;
        _settings.Validate();
        RaiseSettingsChanged(SettingsChangeKind.Size);
    }

    private void RaiseSettingsChanged(SettingsChangeKind kind) =>
        SettingsChanged?.Invoke(this, new SettingsChangedEventArgs(kind));

    private void OnDefaultSize(object sender, RoutedEventArgs e) => ScaleSlider.Value = PetSettings.DefaultSizeScale;

    private void OnResetPosition(object sender, RoutedEventArgs e)
    {
        _settings.Left = null;
        _settings.Top = null;
        RaiseSettingsChanged(SettingsChangeKind.Position);
    }

    private void OnAbout(object sender, RoutedEventArgs e) =>
        WpfMessageBox.Show("miniGaeul\n.NET 8 WPF\n角色：01_gaeul_kitsch", "关于",
            MessageBoxButton.OK, MessageBoxImage.Information);

    private void OnExit(object sender, RoutedEventArgs e)
    {
        if (ExitRequested is not null) ExitRequested.Invoke(this, EventArgs.Empty);
        else WpfApplication.Current.Shutdown();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private static string GetInteractionDisplayName(string name) => name switch
    {
        "interact_think_question" => "思考问号",
        "interact_angry" => "生气",
        "interact_wave" => "挥手",
        "interact_happy_jump" => "开心跳跃",
        "interact_hands_clasp" => "双手相握",
        "interact_wink" => "wink",
        "interact_clasp_sway" => "合手摇摆",
        "interact_arms_crossed" => "双臂交叉",
        "move_right" => "向右走",
        "move_left" => "向左走",
        _ => name
    };

    private sealed record InteractionOption(
        string DisplayName,
        AnimationDefinition Animation);
}
