using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Nexo.Core.Vision;

namespace Nexo.App;

/// <summary>
/// Diseño D5.7 (Fase 2 — Kohana Lens) — dibuja recuadros sobre las regiones que
/// <see cref="LensHighlightMatcher"/> identificó, posicionados en coordenadas reales de pantalla
/// sobre la ventana observada. Se autodescarta sola tras unos segundos: es guía visual pasajera,
/// no un estado que el usuario tenga que cerrar a mano.
/// </summary>
public partial class LensHighlightOverlay : Window
{
    private const int GwlExStyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExTransparent = 0x00000020;

    private static readonly TimeSpan AutoHideDelay = TimeSpan.FromSeconds(6);

    private readonly DispatcherTimer _autoHideTimer;

    public LensHighlightOverlay()
    {
        InitializeComponent();

        _autoHideTimer = new DispatcherTimer { Interval = AutoHideDelay };
        _autoHideTimer.Tick += (_, _) =>
        {
            _autoHideTimer.Stop();
            Hide();
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;
        var styles = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, styles | WsExNoActivate | WsExToolWindow | WsExTransparent);
    }

    public void ShowHighlights(
        int windowLeft,
        int windowTop,
        int windowWidth,
        int windowHeight,
        IReadOnlyList<LensHighlightRegion> regions)
    {
        ArgumentNullException.ThrowIfNull(regions);

        _autoHideTimer.Stop();
        HighlightCanvas.Children.Clear();

        if (regions.Count == 0 || windowWidth <= 0 || windowHeight <= 0)
        {
            Hide();
            return;
        }

        Left = windowLeft;
        Top = windowTop;
        Width = windowWidth;
        Height = windowHeight;

        foreach (var region in regions)
        {
            var rectangle = new Rectangle
            {
                Width = Math.Max(0, region.Width),
                Height = Math.Max(0, region.Height),
                RadiusX = 4,
                RadiusY = 4,
                Stroke = Brushes.Gold,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(60, 255, 215, 0))
            };
            Canvas.SetLeft(rectangle, region.Left - windowLeft);
            Canvas.SetTop(rectangle, region.Top - windowTop);
            HighlightCanvas.Children.Add(rectangle);
        }

        Show();
        _autoHideTimer.Start();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr windowHandle, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr windowHandle, int index, int newLong);
}
