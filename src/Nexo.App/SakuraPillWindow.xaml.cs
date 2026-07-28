using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Nexo.App.Ambient;
using Nexo.Core.Ambient;

namespace Nexo.App;

public partial class SakuraPillWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;

    /// <summary>
    /// Diseño D4 (corrección post smoke test manual, 2026-07-28) — cuánto se muestra un resultado
    /// u error antes de descartarse solo. El usuario reportó que el pill se quedaba abierto de
    /// forma indefinida: a diferencia de <c>CapsuleWindow</c> (aviso transitorio con auto-descarte
    /// desde D1), esta primera versión de D4 nunca tuvo temporizador propio.
    /// </summary>
    private static readonly TimeSpan AutoDismissDelay = TimeSpan.FromSeconds(8);

    private readonly DispatcherTimer _autoDismissTimer;
    private bool _canCancel;
    private bool _canDismiss;
    private bool _isExpanded;
    private Guid? _expandedForRequestId;
    private Guid? _autoDismissForRequestId;
    private AmbientRequestStatus? _autoDismissForStatus;

    public SakuraPillWindow()
    {
        InitializeComponent();

        _autoDismissTimer = new DispatcherTimer { Interval = AutoDismissDelay };
        _autoDismissTimer.Tick += (_, _) =>
        {
            _autoDismissTimer.Stop();
            if (_canDismiss)
            {
                DismissRequested?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    public event EventHandler? CancelRequested;

    public event EventHandler? DismissRequested;

    public event EventHandler? UndoRequested;

    public event EventHandler<string>? QuickActionRequested;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;
        var styles = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, styles | WsExNoActivate | WsExToolWindow);
    }

    public void Apply(AmbientRequestDisplayState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!state.IsVisible)
        {
            _autoDismissTimer.Stop();
            _autoDismissForRequestId = null;
            _autoDismissForStatus = null;
            Hide();
            return;
        }

        if (state.RequestId != _expandedForRequestId)
        {
            _isExpanded = false;
            _expandedForRequestId = state.RequestId;
        }

        _canCancel = state.CanCancel;
        _canDismiss = state.CanDismiss;

        StatusTextBlock.Text = state.StatusText;
        ShortTextBlock.Text = state.ShortText ?? string.Empty;
        ShortTextBlock.Visibility = string.IsNullOrEmpty(state.ShortText)
            ? Visibility.Collapsed
            : Visibility.Visible;

        ErrorTextBlock.Text = state.ErrorMessage ?? string.Empty;
        ErrorTextBlock.Visibility = string.IsNullOrEmpty(state.ErrorMessage)
            ? Visibility.Collapsed
            : Visibility.Visible;

        var hasExpandedText = !string.IsNullOrEmpty(state.ExpandedText);
        ExpandedTextBlock.Text = state.ExpandedText ?? string.Empty;
        ExpandedTextBlock.Visibility = hasExpandedText && _isExpanded
            ? Visibility.Visible
            : Visibility.Collapsed;

        ApplyQuickActions(state.QuickActions);

        ExpandToggleButton.Visibility = hasExpandedText ? Visibility.Visible : Visibility.Collapsed;
        ExpandToggleButton.Content = _isExpanded ? "Contraer" : "Expandir";
        UndoButton.Visibility = state.CanUndo ? Visibility.Visible : Visibility.Collapsed;
        FooterPanel.Visibility = hasExpandedText || state.CanUndo
            ? Visibility.Visible
            : Visibility.Collapsed;

        CloseButton.Visibility = state.CanCancel || state.CanDismiss
            ? Visibility.Visible
            : Visibility.Collapsed;
        CloseButton.ToolTip = state.CanCancel ? "Cancelar" : "Cerrar";

        var description = string.IsNullOrEmpty(state.ErrorMessage)
            ? $"{state.StatusText}. {state.ShortText}"
            : $"{state.StatusText}. {state.ErrorMessage}";
        AutomationProperties.SetName(PillBorder, description);

        if (!state.CanDismiss)
        {
            // Escuchando/Pensando: nunca se autodescarta un ciclo en curso.
            _autoDismissTimer.Stop();
            _autoDismissForRequestId = null;
            _autoDismissForStatus = null;
        }
        else if (state.RequestId != _autoDismissForRequestId || state.Status != _autoDismissForStatus)
        {
            // Resultado/Error nuevo (o recién alcanzable): arranca el auto-descarte desde cero.
            // Si ya se estaba contando para el mismo estado, no se reinicia — así un refresco
            // redundante no extiende el tiempo de vida indefinidamente.
            _autoDismissForRequestId = state.RequestId;
            _autoDismissForStatus = state.Status;
            _autoDismissTimer.Stop();
            _autoDismissTimer.Start();
        }

        if (!IsVisible)
        {
            PositionWindow();
            Show();
        }
    }

    private void ApplyQuickActions(IReadOnlyList<Core.Ambient.AmbientQuickAction> actions)
    {
        QuickActionsPanel.Children.Clear();
        foreach (var action in actions)
        {
            var button = new Button
            {
                Content = action.Label,
                Style = (Style)FindResource("SecondaryButtonStyle"),
                Padding = new Thickness(10, 4, 10, 4),
                FontSize = 12,
                Margin = new Thickness(QuickActionsPanel.Children.Count == 0 ? 0 : 8, 0, 0, 0),
                Tag = action.Id
            };
            AutomationProperties.SetName(button, action.Label);
            button.Click += (_, _) =>
            {
                RestartAutoDismissIfDismissible();
                QuickActionRequested?.Invoke(this, action.Id);
            };
            QuickActionsPanel.Children.Add(button);
        }

        QuickActionsPanel.Visibility = actions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PositionWindow()
    {
        var workArea = SystemParameters.WorkArea;
        Top = workArea.Top + 24;
        Left = workArea.Left + Math.Max(0, (workArea.Width - Width) / 2);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_canCancel)
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (_canDismiss)
        {
            DismissRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ExpandToggleButton_Click(object sender, RoutedEventArgs e)
    {
        _isExpanded = !_isExpanded;
        ExpandedTextBlock.Visibility = _isExpanded ? Visibility.Visible : Visibility.Collapsed;
        ExpandToggleButton.Content = _isExpanded ? "Contraer" : "Expandir";
        RestartAutoDismissIfDismissible();
    }

    /// <summary>
    /// Diseño D4 (corrección post smoke test) — interactuar con un resultado ya visible (expandir,
    /// una acción rápida) le da al usuario una ventana completa nueva antes del auto-descarte, en
    /// vez de dejar que el temporizador ya en curso lo cierre a mitad de la interacción.
    /// </summary>
    private void RestartAutoDismissIfDismissible()
    {
        if (_canDismiss)
        {
            _autoDismissTimer.Stop();
            _autoDismissTimer.Start();
        }
    }

    private void UndoButton_Click(object sender, RoutedEventArgs e) =>
        UndoRequested?.Invoke(this, EventArgs.Empty);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr windowHandle, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr windowHandle, int index, int newLong);
}
