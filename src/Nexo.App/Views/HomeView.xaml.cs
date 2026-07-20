using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Nexo.App.Views;

public partial class HomeView : UserControl
{
    private readonly ObservableCollection<HomeRecentAction> _recentActions = [];

    public HomeView()
    {
        InitializeComponent();
        RecentItems.ItemsSource = _recentActions;
        UpdateRecentVisibility();
    }

    public event EventHandler? CommandRequested;

    public event EventHandler? TasksRequested;

    public event EventHandler? FocusRequested;

    public event EventHandler? ContextRequested;

    public void Refresh(HomeDashboardViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        GreetingText.Text = model.Greeting;
        GreetingDetailText.Text = model.GreetingDetail;
        TaskCountText.Text = model.TaskValue;
        TaskDetailText.Text = model.TaskDetail;
        FocusValueText.Text = model.FocusValue;
        FocusDetailText.Text = model.FocusDetail;
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

    private void ContextCard_Click(object sender, RoutedEventArgs e) =>
        ContextRequested?.Invoke(this, EventArgs.Empty);
}

public sealed record HomeDashboardViewModel(
    string Greeting,
    string GreetingDetail,
    string TaskValue,
    string TaskDetail,
    string FocusValue,
    string FocusDetail,
    string ContextTitle,
    string ContextDetail);

public sealed record HomeRecentAction(
    string Title,
    string Detail,
    string Time);
