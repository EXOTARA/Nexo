using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Nexo.Core.Diagnostics;

namespace Nexo.App;

public sealed class CommandPalettePromptEventArgs(string prompt) : EventArgs
{
    public string Prompt { get; } = prompt;
}

public partial class CommandPaletteWindow : Window
{
    private static readonly string[] BuiltInSuggestions =
    [
        "Abre Visual Studio Code",
        "Silencia el sistema",
        "Baja Spotify al 20 %",
        "Inicia enfoque durante 25 minutos",
        "¿Qué tengo pendiente hoy?",
        "Mira esta ventana",
        "Abre Descargas",
        "Ejecuta mi rutina de estudio"
    ];

    private readonly CommandPaletteStateStore _stateStore = new();
    private readonly CommandPaletteState _state;
    private bool _isExpanded;
    private bool _customizationVisible;
    private bool _isHiding;
    private bool _shellAnimationsEnabled = true;

    public CommandPaletteWindow()
    {
        InitializeComponent();
        _state = _stateStore.Load();
        ReduceMotionCheckBox.IsChecked = _state.ReduceMotion;
        UpdateMotionSelection();
        RefreshSuggestions();
    }

    public event EventHandler<CommandPalettePromptEventArgs>? PromptSubmitted;

    public event EventHandler? WorkspaceRequested;

    public void ShowPalette(bool shellAnimationsEnabled)
    {
        _shellAnimationsEnabled = shellAnimationsEnabled;
        _isHiding = false;
        _customizationVisible = false;
        CustomizationSurface.Visibility = Visibility.Collapsed;
        PromptTextBox.Text = string.Empty;
        SetExpanded(false, animate: false);
        PositionPalette();

        var stopwatch = Stopwatch.StartNew();
        if (!IsVisible)
        {
            Show();
        }

        Activate();
        Topmost = true;
        PromptTextBox.Focus();
        Keyboard.Focus(PromptTextBox);

        ApplyShowMotion();
        Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() =>
            {
                stopwatch.Stop();
                TimingText.Text = $"Listo en {stopwatch.ElapsedMilliseconds} ms";
                WritePerformanceSample(stopwatch.ElapsedMilliseconds);
            }));
    }

    public void HidePalette(bool immediate = false)
    {
        if (!IsVisible || _isHiding)
        {
            return;
        }

        if (immediate || !ShouldAnimate())
        {
            HideImmediately();
            return;
        }

        _isHiding = true;
        var settings = ResolveMotionSettings(isHiding: true);
        var easing = new CubicEase { EasingMode = EasingMode.EaseIn };
        var duration = TimeSpan.FromMilliseconds(settings.DurationMilliseconds);

        var opacityAnimation = new DoubleAnimation(0, duration)
        {
            EasingFunction = easing
        };
        opacityAnimation.Completed += (_, _) => HideImmediately();

        RootBorder.BeginAnimation(OpacityProperty, opacityAnimation);
        PaletteTranslate.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(settings.Offset, duration)
            {
                EasingFunction = easing
            });
        PaletteScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.992, duration)
            {
                EasingFunction = easing
            });
        PaletteScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.992, duration)
            {
                EasingFunction = easing
            });
    }

    private void HideImmediately()
    {
        RootBorder.BeginAnimation(OpacityProperty, null);
        PaletteTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        PaletteScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        PaletteScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        RootBorder.Opacity = 0;
        PaletteTranslate.Y = -8;
        PaletteScale.ScaleX = 0.99;
        PaletteScale.ScaleY = 0.99;
        _isHiding = false;
        Hide();
    }

    private void ApplyShowMotion()
    {
        RootBorder.BeginAnimation(OpacityProperty, null);
        PaletteTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        PaletteScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        PaletteScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

        if (!ShouldAnimate())
        {
            RootBorder.Opacity = 1;
            PaletteTranslate.Y = 0;
            PaletteScale.ScaleX = 1;
            PaletteScale.ScaleY = 1;
            return;
        }

        var settings = ResolveMotionSettings(isHiding: false);
        var duration = TimeSpan.FromMilliseconds(settings.DurationMilliseconds);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        RootBorder.Opacity = 0;
        PaletteTranslate.Y = -settings.Offset;
        PaletteScale.ScaleX = settings.StartScale;
        PaletteScale.ScaleY = settings.StartScale;

        RootBorder.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(1, duration)
            {
                EasingFunction = easing
            });
        PaletteTranslate.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(0, duration)
            {
                EasingFunction = easing
            });
        PaletteScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation(1, duration)
            {
                EasingFunction = easing
            });
        PaletteScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation(1, duration)
            {
                EasingFunction = easing
            });
    }

    private void PositionPalette()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + Math.Max(16, (workArea.Width - Width) / 2);
        Top = workArea.Top + Math.Max(28, Math.Min(132, workArea.Height * 0.14));
    }

    private void PromptTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        PlaceholderText.Visibility = string.IsNullOrEmpty(PromptTextBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;

        RefreshSuggestions();
        if (!string.IsNullOrWhiteSpace(PromptTextBox.Text))
        {
            SetExpanded(true, animate: true);
        }
    }

    private void RefreshSuggestions()
    {
        var query = (PromptTextBox?.Text ?? string.Empty).Trim();
        var combined = _state.RecentCommands
            .Concat(BuiltInSuggestions)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(query))
        {
            combined = combined
                .Select(command => new
                {
                    Command = command,
                    Score = ScoreSuggestion(command, query)
                })
                .Where(item => item.Score > 0)
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Command.Length)
                .Select(item => item.Command);
        }

        var suggestions = combined.Take(7).ToArray();
        SuggestionsList.ItemsSource = suggestions;
        SuggestionsList.SelectedIndex = suggestions.Length > 0 ? 0 : -1;
    }

    private static int ScoreSuggestion(string command, string query)
    {
        if (command.StartsWith(query, StringComparison.CurrentCultureIgnoreCase))
        {
            return 100;
        }

        if (command.Contains(query, StringComparison.CurrentCultureIgnoreCase))
        {
            return 70;
        }

        var queryWords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var matches = queryWords.Count(word =>
            command.Contains(word, StringComparison.CurrentCultureIgnoreCase));
        return matches == 0 ? 0 : 20 + matches * 10;
    }

    private void ExpandButton_Click(object sender, RoutedEventArgs e)
    {
        SetExpanded(!_isExpanded, animate: true);
        PromptTextBox.Focus();
    }

    private void CustomizeButton_Click(object sender, RoutedEventArgs e)
    {
        _customizationVisible = !_customizationVisible;
        CustomizationSurface.Visibility = _customizationVisible
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (_customizationVisible)
        {
            AnimateSurfaceIn(CustomizationSurface);
        }

        PromptTextBox.Focus();
    }

    private void SetExpanded(bool expanded, bool animate)
    {
        _isExpanded = expanded;
        ExpandedSurface.Visibility = expanded
            ? Visibility.Visible
            : Visibility.Collapsed;
        ExpandButton.Content = expanded ? "⌃" : "⌄";

        if (expanded && animate)
        {
            AnimateSurfaceIn(ExpandedSurface);
        }
    }

    private void AnimateSurfaceIn(UIElement surface)
    {
        if (!ShouldAnimate())
        {
            surface.Opacity = 1;
            return;
        }

        surface.Opacity = 0;
        surface.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(1, TimeSpan.FromMilliseconds(120))
            {
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            });
    }

    private void SuggestionsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        SubmitSelectedSuggestion();
    }

    private void SubmitSelectedSuggestion()
    {
        if (SuggestionsList.SelectedItem is string selected)
        {
            SubmitPrompt(selected);
        }
    }

    private void SubmitPrompt(string? preferredPrompt = null)
    {
        var prompt = string.IsNullOrWhiteSpace(preferredPrompt)
            ? PromptTextBox.Text.Trim()
            : preferredPrompt.Trim();

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        _stateStore.Remember(_state, prompt);
        PromptSubmitted?.Invoke(this, new CommandPalettePromptEventArgs(prompt));
        HidePalette();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HidePalette();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Tab)
        {
            SetExpanded(!_isExpanded, animate: true);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            WorkspaceRequested?.Invoke(this, EventArgs.Empty);
            HidePalette();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            SubmitPrompt();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down && _isExpanded && SuggestionsList.Items.Count > 0)
        {
            SuggestionsList.SelectedIndex = Math.Min(
                SuggestionsList.Items.Count - 1,
                SuggestionsList.SelectedIndex + 1);
            SuggestionsList.ScrollIntoView(SuggestionsList.SelectedItem);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up && _isExpanded && SuggestionsList.Items.Count > 0)
        {
            SuggestionsList.SelectedIndex = Math.Max(
                0,
                SuggestionsList.SelectedIndex - 1);
            SuggestionsList.ScrollIntoView(SuggestionsList.SelectedItem);
            e.Handled = true;
        }
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        HidePalette();
    }

    private void MotionPresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string value } ||
            !Enum.TryParse<ShellMotionPreset>(value, ignoreCase: true, out var preset))
        {
            return;
        }

        _state.MotionPreset = preset;
        _state.ReduceMotion = preset == ShellMotionPreset.None ||
            ReduceMotionCheckBox.IsChecked == true;
        ReduceMotionCheckBox.IsChecked = _state.ReduceMotion;
        _stateStore.Save(_state);
        UpdateMotionSelection();
        ApplyShowMotion();
        PromptTextBox.Focus();
    }

    private void ReduceMotionCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _state.ReduceMotion = ReduceMotionCheckBox.IsChecked == true;
        _stateStore.Save(_state);
        UpdateMotionSelection();
        PromptTextBox.Focus();
    }

    private void UpdateMotionSelection()
    {
        var defaultBrush = TryFindResource("BrushSurfaceRaised") as Brush;
        var selectedBrush = TryFindResource("BrushAccentSoft") as Brush;
        var buttons = new[]
        {
            FluidMotionButton,
            SnappyMotionButton,
            CalmMotionButton,
            NoMotionButton
        };

        foreach (var button in buttons)
        {
            button.Background = defaultBrush;
            button.Foreground = TryFindResource("BrushTextSecondary") as Brush;
        }

        var selected = _state.MotionPreset switch
        {
            ShellMotionPreset.Snappy => SnappyMotionButton,
            ShellMotionPreset.Calm => CalmMotionButton,
            ShellMotionPreset.None => NoMotionButton,
            _ => FluidMotionButton
        };
        selected.Background = selectedBrush;
        selected.Foreground = TryFindResource("BrushAccent") as Brush;
    }

    private bool ShouldAnimate() =>
        _shellAnimationsEnabled &&
        !_state.ReduceMotion &&
        _state.MotionPreset != ShellMotionPreset.None;

    private MotionSettings ResolveMotionSettings(bool isHiding)
    {
        return _state.MotionPreset switch
        {
            ShellMotionPreset.Snappy => new MotionSettings(
                isHiding ? 78 : 108,
                6,
                0.992),
            ShellMotionPreset.Calm => new MotionSettings(
                isHiding ? 180 : 260,
                8,
                0.989),
            ShellMotionPreset.None => new MotionSettings(0, 0, 1),
            _ => new MotionSettings(
                isHiding ? 118 : 185,
                10,
                0.985)
        };
    }

    private static void WritePerformanceSample(long elapsedMilliseconds)
    {
        try
        {
            var logDirectory = Path.Combine(NexoDataPaths.RootDirectory, "Logs");
            Directory.CreateDirectory(logDirectory);
            File.AppendAllText(
                Path.Combine(logDirectory, "performance.log"),
                $"{DateTimeOffset.Now:O}\tcommand_palette_visible\t{elapsedMilliseconds}ms{Environment.NewLine}");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // La medición nunca debe impedir que la paleta se abra.
        }
    }

    private readonly record struct MotionSettings(
        double DurationMilliseconds,
        double Offset,
        double StartScale);
}
