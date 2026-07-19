using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Nexo.Core.Vision;

namespace Nexo.App;

public partial class VisionPreviewWindow : Window
{
    private readonly IVisionOcrService _ocrService;
    private readonly byte[] _originalPngBytes;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private BitmapSource _currentBitmap;
    private byte[] _currentPngBytes;
    private Point _selectionStart;
    private bool _selectionMode;
    private bool _isSelecting;
    private bool _requiresPrivacyReview;

    public VisionPreviewWindow(
        string sourceTitle,
        byte[] pngBytes,
        IVisionOcrService ocrService)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        ArgumentNullException.ThrowIfNull(ocrService);

        InitializeComponent();
        _ocrService = ocrService;
        _originalPngBytes = pngBytes.ToArray();
        _currentPngBytes = pngBytes.ToArray();
        _currentBitmap = LoadBitmap(_currentPngBytes);
        SourceTitleText.Text = sourceTitle;
        PreviewImage.Source = _currentBitmap;

        Loaded += (_, _) =>
    {
    OcrStatusText.Text =
        "OCR local desactivado temporalmente. Puedes continuar usando la captura con la IA.";
    };
        Closed += (_, _) => _lifetimeCancellation.Cancel();
    }

    public byte[] SelectedPngBytes => _currentPngBytes.ToArray();

    public string ExtractedText { get; private set; } = string.Empty;

    public bool KeepForConversation => KeepCaptureCheckBox.IsChecked == true;

    private void UseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_requiresPrivacyReview && PrivacyReviewedCheckBox.IsChecked != true)
        {
            return;
        }

        DialogResult = true;
    }

    private void DiscardButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void SelectAreaButton_Click(object sender, RoutedEventArgs e)
    {
        _selectionMode = true;
        SelectionCanvas.IsHitTestVisible = true;
        SelectionRectangle.Visibility = Visibility.Collapsed;
        ApplySelectionButton.IsEnabled = false;
        OcrStatusText.Text = "Arrastra sobre la imagen para marcar el área que deseas conservar.";
    }

    private void ApplySelectionButton_Click(object sender, RoutedEventArgs e)
{
    var cropRectangle = GetSelectionPixelRectangle();
    if (cropRectangle is null)
    {
        return;
    }

    try
    {
        var cropped = new CroppedBitmap(
            _currentBitmap,
            cropRectangle.Value);

        cropped.Freeze();

        _currentBitmap = cropped;
        _currentPngBytes = EncodePng(cropped);
        PreviewImage.Source = cropped;

        EndSelectionMode();

        ExtractedText = string.Empty;
        OcrTextBox.Text =
            "El recorte está listo para analizarse con la IA.";

        OcrStatusText.Text =
            "Recorte aplicado. OCR local desactivado temporalmente.";
    }
    catch (Exception exception)
    {
        EndSelectionMode();

        OcrStatusText.Text =
            $"No pude aplicar el recorte: {exception.Message}";
    }
    }

    private void ResetImageButton_Click(object sender, RoutedEventArgs e)
{
    try
    {
        _currentPngBytes = _originalPngBytes.ToArray();
        _currentBitmap = LoadBitmap(_currentPngBytes);
        PreviewImage.Source = _currentBitmap;

        EndSelectionMode();

        ExtractedText = string.Empty;
        OcrTextBox.Text =
            "La captura completa está lista para analizarse con la IA.";

        OcrStatusText.Text =
            "Imagen original restaurada. OCR local desactivado temporalmente.";
    }
    catch (Exception exception)
    {
        OcrStatusText.Text =
            $"No pude restaurar la captura: {exception.Message}";
    }
    }

    private void SelectionCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_selectionMode)
        {
            return;
        }

        _selectionStart = ClampToRenderedImage(e.GetPosition(SelectionCanvas));
        _isSelecting = true;
        SelectionCanvas.CaptureMouse();
        SelectionRectangle.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionRectangle, _selectionStart.X);
        Canvas.SetTop(SelectionRectangle, _selectionStart.Y);
        SelectionRectangle.Width = 0;
        SelectionRectangle.Height = 0;
        e.Handled = true;
    }

    private void SelectionCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isSelecting || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = ClampToRenderedImage(e.GetPosition(SelectionCanvas));
        UpdateSelectionRectangle(_selectionStart, current);
    }

    private void SelectionCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSelecting)
        {
            return;
        }

        var current = ClampToRenderedImage(e.GetPosition(SelectionCanvas));
        UpdateSelectionRectangle(_selectionStart, current);
        _isSelecting = false;
        SelectionCanvas.ReleaseMouseCapture();
        ApplySelectionButton.IsEnabled =
            SelectionRectangle.Width >= 12 && SelectionRectangle.Height >= 12;
        e.Handled = true;
    }

    private void UpdateSelectionRectangle(Point start, Point end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        Canvas.SetLeft(SelectionRectangle, left);
        Canvas.SetTop(SelectionRectangle, top);
        SelectionRectangle.Width = Math.Abs(end.X - start.X);
        SelectionRectangle.Height = Math.Abs(end.Y - start.Y);
    }

    private Int32Rect? GetSelectionPixelRectangle()
    {
        if (SelectionRectangle.Visibility != Visibility.Visible ||
            SelectionRectangle.Width < 2 ||
            SelectionRectangle.Height < 2)
        {
            return null;
        }

        var renderedBounds = GetRenderedImageBounds();
        if (renderedBounds.Width <= 0 || renderedBounds.Height <= 0)
        {
            return null;
        }

        var selection = new Rect(
            Canvas.GetLeft(SelectionRectangle),
            Canvas.GetTop(SelectionRectangle),
            SelectionRectangle.Width,
            SelectionRectangle.Height);
        selection.Intersect(renderedBounds);
        if (selection.IsEmpty)
        {
            return null;
        }

        var scaleX = _currentBitmap.PixelWidth / renderedBounds.Width;
        var scaleY = _currentBitmap.PixelHeight / renderedBounds.Height;
        var x = Math.Clamp(
            (int)Math.Floor((selection.Left - renderedBounds.Left) * scaleX),
            0,
            _currentBitmap.PixelWidth - 1);
        var y = Math.Clamp(
            (int)Math.Floor((selection.Top - renderedBounds.Top) * scaleY),
            0,
            _currentBitmap.PixelHeight - 1);
        var right = Math.Clamp(
            (int)Math.Ceiling((selection.Right - renderedBounds.Left) * scaleX),
            x + 1,
            _currentBitmap.PixelWidth);
        var bottom = Math.Clamp(
            (int)Math.Ceiling((selection.Bottom - renderedBounds.Top) * scaleY),
            y + 1,
            _currentBitmap.PixelHeight);
        return new Int32Rect(x, y, right - x, bottom - y);
    }

    private Point ClampToRenderedImage(Point point)
    {
        var bounds = GetRenderedImageBounds();
        return new Point(
            Math.Clamp(point.X, bounds.Left, bounds.Right),
            Math.Clamp(point.Y, bounds.Top, bounds.Bottom));
    }

    private Rect GetRenderedImageBounds()
    {
        if (_currentBitmap.PixelWidth <= 0 ||
            _currentBitmap.PixelHeight <= 0 ||
            PreviewSurface.ActualWidth <= 0 ||
            PreviewSurface.ActualHeight <= 0)
        {
            return Rect.Empty;
        }

        var scale = Math.Min(
            PreviewSurface.ActualWidth / _currentBitmap.PixelWidth,
            PreviewSurface.ActualHeight / _currentBitmap.PixelHeight);
        var width = _currentBitmap.PixelWidth * scale;
        var height = _currentBitmap.PixelHeight * scale;
        return new Rect(
            (PreviewSurface.ActualWidth - width) / 2,
            (PreviewSurface.ActualHeight - height) / 2,
            width,
            height);
    }

    private void EndSelectionMode()
    {
        _selectionMode = false;
        _isSelecting = false;
        SelectionCanvas.IsHitTestVisible = false;
        SelectionCanvas.ReleaseMouseCapture();
        SelectionRectangle.Visibility = Visibility.Collapsed;
        ApplySelectionButton.IsEnabled = false;
    }

    private async Task RunOcrAsync()
    {
        OcrStatusText.Text = "Extrayendo texto localmente…";
        OcrTextBox.Text = string.Empty;
        ExtractedText = string.Empty;
        PrivacyWarningPanel.Visibility = Visibility.Collapsed;
        PrivacyReviewedCheckBox.IsChecked = false;
        _requiresPrivacyReview = false;
        UseButton.IsEnabled = true;

        VisionOcrResult result;
        try
        {
            result = await _ocrService.RecognizeAsync(
                _currentPngBytes,
                _lifetimeCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!result.IsSuccess)
        {
            OcrStatusText.Text = result.Detail;
            return;
        }

        ExtractedText = result.Text;
        OcrTextBox.Text = string.IsNullOrWhiteSpace(result.Text)
            ? "No se encontró texto legible."
            : result.Text;
        OcrStatusText.Text = string.IsNullOrWhiteSpace(result.Text)
            ? result.Detail
            : $"Confianza aproximada: {result.Confidence:P0}. El texto se procesó en tu equipo.";

        var findings = VisionTextPrivacyPolicy.Analyze(result.Text);
        if (findings.Count == 0)
        {
            return;
        }

        _requiresPrivacyReview = true;
        UseButton.IsEnabled = false;
        PrivacyWarningPanel.Visibility = Visibility.Visible;
        PrivacyWarningText.Text =
            "Revisa la captura antes de usarla. Nexo detectó: " +
            string.Join(", ", findings.Select(finding =>
                $"{finding.Description} ({finding.Count})")) + ".";
    }

    private void PrivacyReviewedCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UseButton.IsEnabled =
            !_requiresPrivacyReview || PrivacyReviewedCheckBox.IsChecked == true;
    }

    private void CopyOcrButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(ExtractedText))
        {
            Clipboard.SetText(ExtractedText);
            OcrStatusText.Text = "Texto copiado al portapapeles.";
        }
    }

    private static BitmapSource LoadBitmap(byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static byte[] EncodePng(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
