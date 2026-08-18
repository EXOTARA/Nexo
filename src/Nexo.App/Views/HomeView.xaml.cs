using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Nexo.App.Views;

public partial class HomeView : UserControl
{
    private readonly ObservableCollection<HomeRecentAction> _recentActions = [];
    private bool _focusIsPaused;

    public HomeView()
    {
        InitializeComponent();
        RecentItems.ItemsSource = _recentActions;
        UpdateRecentVisibility();
    }

    public event EventHandler? CommandRequested;

    public event EventHandler? TasksRequested;

    public event EventHandler? FocusRequested;

    public event EventHandler? RoutinesRequested;

    public event EventHandler? ContextRequested;

    /// <summary>Diseño D3 — fila de accesos rápidos: "Nueva tarea".</summary>
    public event EventHandler? NewTaskRequested;

    /// <summary>Diseño D3 — fila de accesos rápidos: "Iniciar enfoque".</summary>
    public event EventHandler? StartFocusRequested;

    /// <summary>
    /// Diseño D3.1 — acción rápida en la tarjeta de Enfoque cuando ya hay una sesión activa:
    /// pausarla sin salir de Inicio.
    /// </summary>
    public event EventHandler? PauseFocusRequested;

    /// <summary>
    /// Diseño D3.1 — acción rápida en la tarjeta de Enfoque cuando la sesión está en pausa:
    /// continuarla sin salir de Inicio.
    /// </summary>
    public event EventHandler? ResumeFocusRequested;

    /// <summary>
    /// Diseño D3 — fila de accesos rápidos: abrir el Sakura Command Center (Ctrl + K). Distinto
    /// de <see cref="CommandRequested"/>, que abre la paleta de prompts de IA (Ctrl + Espacio) —
    /// son cosas diferentes, igual que ya lo son sus atajos.
    /// </summary>
    public event EventHandler? CommandCenterRequested;

    /// <summary>
    /// Diseño D51 — la insignia de pendientes aparece solo cuando hay algo pendiente.
    ///
    /// Es la nota del PDF sobre que Pendientes debe ser discreto, y la regla es la de cualquier
    /// notificación: sin nada que contar, no hay insignia. Enseñar un cero es ocupar sitio para
    /// decir que no pasa nada.
    ///
    /// El recuento llega ya formateado como texto desde el resumen del día, así que aquí no se
    /// vuelve a interpretar: basta con distinguir «nada» de «algo». Un valor que no sea un número
    /// —porque el resumen decida decir otra cosa— se enseña igual; lo que se oculta es el cero.
    /// </summary>
    private void ApplyTaskBadge(string? value)
    {
        var text = value?.Trim() ?? string.Empty;

        var nothingPending =
            text.Length == 0 ||
            (int.TryParse(text, out var count) && count == 0);

        TaskCountText.Text = text;
        TaskCountBadge.Visibility = nothingPending ? Visibility.Collapsed : Visibility.Visible;
    }

    public void Refresh(HomeDashboardViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        GreetingText.Text = model.Greeting;
        GreetingDetailText.Text = model.GreetingDetail;
        ApplyTaskBadge(model.TaskValue);
        TaskDetailText.Text = model.TaskDetail;
        FocusValueText.Text = model.FocusValue;
        FocusDetailText.Text = model.FocusDetail;

        _focusIsPaused = model.FocusIsPaused;
        if (model.FocusHasActiveSession)
        {
            FocusQuickActionButton.Content = _focusIsPaused ? "Continuar" : "Pausar";
            FocusQuickActionButton.Visibility = Visibility.Visible;
            AutomationProperties.SetName(
                FocusQuickActionButton,
                _focusIsPaused ? "Continuar sesión de enfoque" : "Pausar sesión de enfoque");
        }
        else
        {
            FocusQuickActionButton.Visibility = Visibility.Collapsed;
        }

        RoutineCountText.Text = model.RoutineValue;
        RoutineDetailText.Text = model.RoutineDetail;
        ContextTitleText.Text = model.ContextTitle;
        ContextDetailText.Text = model.ContextDetail;
    }

    public void AddRecentAction(string title, string detail)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        _recentActions.Insert(
            0,
            new HomeRecentAction(
                title.Trim(),
                detail?.Trim() ?? string.Empty,
                DateTime.Now.ToString("HH:mm")));

        while (_recentActions.Count > 4)
        {
            _recentActions.RemoveAt(_recentActions.Count - 1);
        }

        UpdateRecentVisibility();
    }

    private void UpdateRecentVisibility()
    {
        EmptyActivityText.Visibility = _recentActions.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        RecentItems.Visibility = _recentActions.Count == 0
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void CommandButton_Click(object sender, RoutedEventArgs e) =>
        CommandRequested?.Invoke(this, EventArgs.Empty);

    private void TasksCard_Click(object sender, RoutedEventArgs e) =>
        TasksRequested?.Invoke(this, EventArgs.Empty);

    private void FocusCard_Click(object sender, RoutedEventArgs e) =>
        FocusRequested?.Invoke(this, EventArgs.Empty);

    private void RoutinesCard_Click(object sender, RoutedEventArgs e) =>
        RoutinesRequested?.Invoke(this, EventArgs.Empty);

    private void ContextCard_Click(object sender, RoutedEventArgs e) =>
        ContextRequested?.Invoke(this, EventArgs.Empty);

    private void NewTaskQuickAction_Click(object sender, RoutedEventArgs e) =>
        NewTaskRequested?.Invoke(this, EventArgs.Empty);

    private void StartFocusQuickAction_Click(object sender, RoutedEventArgs e) =>
        StartFocusRequested?.Invoke(this, EventArgs.Empty);

    private void FocusQuickActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_focusIsPaused)
        {
            ResumeFocusRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            PauseFocusRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void CommandCenterQuickAction_Click(object sender, RoutedEventArgs e) =>
        CommandCenterRequested?.Invoke(this, EventArgs.Empty);
}

public sealed record HomeDashboardViewModel(
    string Greeting,
    string GreetingDetail,
    string TaskValue,
    string TaskDetail,
    string FocusValue,
    string FocusDetail,
    bool FocusHasActiveSession,
    bool FocusIsPaused,
    string RoutineValue,
    string RoutineDetail,
    string ContextTitle,
    string ContextDetail);

public sealed record HomeRecentAction(
    string Title,
    string Detail,
    string Time);
