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

public sealed record CommandPaletteSuggestion(
    string Title,
    string Command,
    string Subtitle,
    string Glyph,
    string ShortcutHint,
    IReadOnlyList<string> Keywords)
{
    public Visibility ShortcutVisibility =>
        string.IsNullOrWhiteSpace(ShortcutHint)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public static CommandPaletteSuggestion Recent(string command) => new(
        command,
        command,
        "Usado recientemente",
        "↺",
        string.Empty,
        [command]);
}

public partial class CommandPaletteWindow : Window
{
    private static readonly CommandPaletteSuggestion[] BuiltInSuggestions =
    [
        new(
            "Explorador de archivos",
            "abre el explorador de archivos",
            "Abrir tus carpetas en Windows",
            "▣",
            "E",
            ["e", "explorador", "archivos", "carpetas", "windows explorer"]),
        new(
            "Descargas",
            "abre descargas",
            "Abrir la carpeta Descargas",
            "↓",
            "D",
            ["d", "descargas", "downloads", "carpeta"]),
        new(
            "Visual Studio Code",
            "abre Visual Studio Code",
            "Abrir el editor de código",
            "⌘",
            "V",
            ["v", "visual studio code", "vscode", "code", "editor"]),
        new(
            "Calculadora",
            "abre la calculadora",
            "Abrir la calculadora de Windows",
            "±",
            "C",
            ["c", "calculadora", "calc", "cuentas"]),
        new(
            "Administrador de tareas",
            "abre el administrador de tareas",
            "Revisar procesos y rendimiento",
            "◎",
            "T",
            ["t", "administrador de tareas", "task manager", "procesos"]),
        new(
            "PowerShell",
            "abre PowerShell",
            "Abrir una terminal en tu carpeta personal",
            ">_",
            "P",
            ["p", "powershell", "terminal", "consola"]),
        new(
            "Mirar esta ventana",
            "mira esta ventana",
            "Capturar y analizar la ventana activa",
            "◫",
            "M",
            ["m", "mirar", "captura", "vision", "ventana"]),
        new(
            "Enfoque de 25 minutos",
            "inicia enfoque durante 25 minutos",
            "Empezar una sesión de concentración",
            "◉",
            "F",
            ["f", "focus", "enfoque", "pomodoro", "25 minutos"]),
        new(
            "Spotify al 20 %",
            "baja Spotify al 20 %",
            "Ajustar el audio localmente",
            "♪",
            string.Empty,
            ["spotify", "audio", "volumen", "20"]),
        new(
            "Pendientes de hoy",
            "¿Qué tengo pendiente hoy?",
            "Consultar tareas y recordatorios",
            "✓",
            string.Empty,
            ["pendientes", "hoy", "tareas", "recordatorios"]),
        new(
            "Rutina de estudio",
            "ejecuta mi rutina de estudio",
            "Ejecutar una rutina guardada",
            "↻",
            string.Empty,
            ["rutina", "estudio", "automatizacion"])
    ];

    private readonly CommandPaletteStateStore _stateStore = new();
    private readonly CommandPaletteState _state;
    private bool _isExpanded;
    private bool _customizationVisible;
    private bool _isHiding;
    private bool _shellAnimationsEnabled = true;
    private bool _isApplyingCompletion;

    public CommandPaletteWindow()
    {
        InitializeComponent();
        _state = _stateStore.Load();
        ReduceMotionCheckBox.IsChecked = _state.ReduceMotion;
        UpdateMotionSelection();
        RefreshSuggestions(string.Empty);
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
        Left = workArea.Left + Math.Max(18, (workArea.Width - Width) / 2);
        Top = workArea.Top + Math.Max(30, Math.Min(136, workArea.Height * 0.14));
    }

    private void PromptTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        PlaceholderText.Visibility = string.IsNullOrEmpty(PromptTextBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (_isApplyingCompletion)
        {
            return;
        }

        var query = PromptTextBox.Text.Trim();
        RefreshSuggestions(query);

        if (!string.IsNullOrWhiteSpace(query))
        {
            SetExpanded(true, animate: true);
            TryApplyShortcutCompletion(query);
        }
    }

    private void RefreshSuggestions(string query)
    {
        var recent = _state.RecentCommands
            .Select(CommandPaletteSuggestion.Recent)
            .ToArray();
        var builtInCommands = BuiltInSuggestions
            .Select(item => item.Command)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var recentCommands = recent
            .Select(item => item.Command)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        IEnumerable<CommandPaletteSuggestion> combined =
            string.IsNullOrWhiteSpace(query)
                ? recent.Concat(BuiltInSuggestions.Where(item =>
                    !recentCommands.Contains(item.Command)))
                : BuiltInSuggestions.Concat(recent.Where(item =>
                    !builtInCommands.Contains(item.Command)));

        if (!string.IsNullOrWhiteSpace(query))
        {
            combined = combined
                .Select(item => new
                {
                    Suggestion = item,
                    Score = ScoreSuggestion(item, query)
                })
                .Where(item => item.Score > 0)
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Suggestion.Title.Length)
                .Select(item => item.Suggestion);
        }

        var suggestions = combined.Take(7).ToArray();
        SuggestionsList.ItemsSource = suggestions;
        SuggestionsList.SelectedIndex = suggestions.Length > 0 ? 0 : -1;
    }

    private static int ScoreSuggestion(
        CommandPaletteSuggestion suggestion,
        string query)
    {
        var normalizedQuery = query.Trim();
        if (normalizedQuery.Length == 0)
        {
            return 1;
        }

        if (suggestion.ShortcutHint.Equals(
                normalizedQuery,
                StringComparison.OrdinalIgnoreCase))
        {
            return 300;
        }

        if (suggestion.Title.StartsWith(
                normalizedQuery,
                StringComparison.CurrentCultureIgnoreCase))
        {
            return 220;
        }

        if (suggestion.Command.StartsWith(
                normalizedQuery,
                StringComparison.CurrentCultureIgnoreCase))
        {
            return 200;
        }

        if (suggestion.Keywords.Any(keyword => keyword.StartsWith(
                normalizedQuery,
                StringComparison.CurrentCultureIgnoreCase)))
        {
            return 170;
        }

        if (suggestion.Title.Contains(
                normalizedQuery,
                StringComparison.CurrentCultureIgnoreCase) ||
            suggestion.Command.Contains(
                normalizedQuery,
                StringComparison.CurrentCultureIgnoreCase))
        {
            return 120;
        }

        var queryWords = normalizedQuery.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var searchable = suggestion.Title + " " + suggestion.Command + " " +
            string.Join(" ", suggestion.Keywords);
        var matches = queryWords.Count(word => searchable.Contains(
            word,
            StringComparison.CurrentCultureIgnoreCase));
        return matches == 0 ? 0 : 40 + matches * 20;
    }

    private void TryApplyShortcutCompletion(string query)
    {
        if (query.Length != 1 ||
            SuggestionsList.SelectedItem is not CommandPaletteSuggestion selected ||
            !selected.ShortcutHint.Equals(query, StringComparison.OrdinalIgnoreCase) ||
            !selected.Title.StartsWith(query, StringComparison.CurrentCultureIgnoreCase))
        {
            return;
        }

        ApplyCompletion(selected, query.Length);
    }

    private void ApplyCompletion(
        CommandPaletteSuggestion suggestion,
        int preservedPrefixLength = 0)
    {
        _isApplyingCompletion = true;
        try
        {
            PromptTextBox.Text = suggestion.Title;
            var prefixLength = Math.Clamp(
                preservedPrefixLength,
                0,
                suggestion.Title.Length);
            PromptTextBox.Select(
                prefixLength,
                suggestion.Title.Length - prefixLength);
        }
        finally
        {
            _isApplyingCompletion = false;
        }
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
        if (_isExpanded == expanded && ExpandedSurface.Visibility ==
            (expanded ? Visibility.Visible : Visibility.Collapsed))
        {
            return;
        }

        _isExpanded = expanded;
        ExpandedSurface.Visibility = expanded
            ? Visibility.Visible
            : Visibility.Collapsed;

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

    private void SuggestionsList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        // La selección se mantiene visualmente; Tab decide si se copia al campo.
    }

    private void SubmitSelectedSuggestion()
    {
        if (SuggestionsList.SelectedItem is CommandPaletteSuggestion selected)
        {
            SubmitPrompt(selected.Command);
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
            if (SuggestionsList.SelectedItem is CommandPaletteSuggestion selected)
            {
                ApplyCompletion(selected);
                PromptTextBox.CaretIndex = PromptTextBox.Text.Length;
            }
            else
            {
                SetExpanded(true, animate: true);
            }

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
            if (_isExpanded &&
                SuggestionsList.SelectedItem is CommandPaletteSuggestion)
            {
                SubmitSelectedSuggestion();
            }
            else
            {
                SubmitPrompt();
            }

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
