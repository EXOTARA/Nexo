using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Nexo.Core.Focus;

namespace Nexo.App.Views;

public partial class FocusView : UserControl
{
    private readonly FocusManager _focusManager;

    public FocusView(FocusManager focusManager)
    {
        _focusManager = focusManager;
        InitializeComponent();
        Refresh(DateTimeOffset.Now);
    }

    public event EventHandler? FocusChanged;

    public void Refresh(DateTimeOffset now)
    {
        var snapshot = _focusManager.GetSnapshot(now);
        var timer = snapshot.ActiveTimer;

        TodaySessionsText.Text = $"{snapshot.CompletedSessionsToday} " +
            (snapshot.CompletedSessionsToday == 1 ? "sesión" : "sesiones");
        TodayMinutesText.Text = $"{snapshot.FocusMinutesToday} min";

        if (timer is null)
        {
            TimerStateText.Text = "LISTO";
            TimerLabelText.Text = "Sin sesión activa";
            RemainingTimeText.Text = "00:00";
            TimerDetailText.Text = "Elige una duración para comenzar.";
            TimerProgressBar.Value = 0;
            PauseButton.IsEnabled = false;
            ResumeButton.IsEnabled = false;
            CancelButton.IsEnabled = false;
            return;
        }

        var remaining = snapshot.Remaining;
        var elapsed = timer.Duration - remaining;
        var progress = timer.Duration.TotalSeconds <= 0
            ? 0
            : Math.Clamp(elapsed.TotalSeconds / timer.Duration.TotalSeconds * 100, 0, 100);

        TimerStateText.Text = timer.Status == FocusTimerStatus.Paused
            ? "EN PAUSA"
            : "EN CURSO";
        TimerLabelText.Text = timer.Label;
        RemainingTimeText.Text = FormatClock(remaining);
        TimerDetailText.Text = timer.Status == FocusTimerStatus.Paused
            ? "La sesión está pausada y conservará este tiempo."
            : $"Finaliza aproximadamente a las {timer.EndsAt:HH:mm}.";
        TimerProgressBar.Value = progress;
        PauseButton.IsEnabled = timer.Status == FocusTimerStatus.Running;
        ResumeButton.IsEnabled = timer.Status == FocusTimerStatus.Paused;
        CancelButton.IsEnabled = true;
    }

    public void FocusPrimaryControl()
    {
        if (_focusManager.GetSnapshot(DateTimeOffset.Now).ActiveTimer is null)
        {
            CustomMinutesTextBox.Focus();
        }
    }

    private void PresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag })
        {
            return;
        }

        var parts = tag.Split(':', 2);
        if (parts.Length != 2 ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes))
        {
            return;
        }

        var kind = parts[0].Equals("Break", StringComparison.OrdinalIgnoreCase)
            ? FocusSessionKind.Break
            : FocusSessionKind.Focus;
        Start(TimeSpan.FromMinutes(minutes), kind);
    }

    private void StartCustomButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(
                CustomMinutesTextBox.Text.Trim(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var minutes) ||
            minutes is < 1 or > 1440)
        {
            ActionStatusText.Text = "Escribe una duración entre 1 y 1440 minutos.";
            return;
        }

        Start(TimeSpan.FromMinutes(minutes), FocusSessionKind.Custom);
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e) =>
        Apply(_focusManager.Pause(DateTimeOffset.Now));

    private void ResumeButton_Click(object sender, RoutedEventArgs e) =>
        Apply(_focusManager.Resume(DateTimeOffset.Now));

    private void CancelButton_Click(object sender, RoutedEventArgs e) =>
        Apply(_focusManager.Cancel());

    private void Start(TimeSpan duration, FocusSessionKind kind)
    {
        var label = kind switch
        {
            FocusSessionKind.Break => "Descanso",
            FocusSessionKind.Focus => "Sesión de enfoque",
            _ => "Temporizador"
        };

        Apply(_focusManager.Start(
            duration,
            label,
            kind,
            DateTimeOffset.Now));
    }

    private void Apply(FocusOperationResult result)
    {
        ActionStatusText.Text = result.Message;
        Refresh(DateTimeOffset.Now);
        FocusChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string FormatClock(TimeSpan remaining)
    {
        var totalHours = (int)remaining.TotalHours;
        return totalHours > 0
            ? $"{totalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}"
            : $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
    }
}
