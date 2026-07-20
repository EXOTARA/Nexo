using System.Windows;
using System.Windows.Controls;

namespace Nexo.App.Views;

public partial class CaptureView : UserControl
{
    public CaptureView()
    {
        InitializeComponent();
    }

    public event EventHandler? CaptureRequested;

    private void CaptureButton_Click(object sender, RoutedEventArgs e)
    {
        CaptureRequested?.Invoke(this, EventArgs.Empty);
    }
}
