using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Nexo.App.Motion;
using Nexo.App.Views.Controls;
using Nexo.Core.Media;
using Nexo.Core.Metrics;
using Nexo.Core.Shell;
using Nexo.Core.Time;

namespace Nexo.App.Views;

/// <summary>
/// Diseño D44 — el contenido del cajón: Panel, Media y Rendimiento bajo la tira de pestañas.
///
/// Vive en su propia vista y no dentro de Sistema porque en la referencia no es una sección de una
/// pantalla, es un cajón que baja del borde de arriba sobre lo que estés haciendo. Sistema se queda
/// con el diagnóstico, que es lo que sí se va a leer sentado.
/// </summary>
public partial class DashboardView : UserControl
{
    private readonly ObservableCollection<CalendarCell> _calendarCells = [];

    private DateOnly _calendarMonth = DateOnly.FromDateTime(DateTime.Now);
    private MediaSnapshot _media = MediaSnapshot.Nothing;
    private DashboardTab _activeTab = DashboardTab.Panel;

    /// <summary>
    /// Diseño D55 — la silueta de pétalos gira despacio mientras suena.
    ///
    /// Una vuelta cada cuarenta segundos. El número importa: la forma tiene ocho pétalos, así que
    /// un giro completo pasa ocho crestas por el mismo punto — a esta velocidad eso es una cada
    /// cinco segundos, que se percibe como que la pieza está viva sin llegar a pedir atención. Más
    /// rápido y una silueta ondulada girando marea; más lento y no se distingue de estar quieta.
    ///
    /// Lo que gira es la FORMA, no la fotografía. La imagen se queda derecha y la flor es su
    /// máscara: es la diferencia entre un disco dando vueltas —que marea y además pone la carátula
    /// del revés cada veinte segundos— y un marco vivo alrededor de una imagen que se puede seguir
    /// mirando.
    ///
    /// El centro de giro se declara en 94,94 y no con RenderTransformOrigin porque la misma
    /// transformación sirve para dos cosas distintas: la máscara de la imagen y el fondo. Una
    /// geometría de recorte no tiene «origen relativo»; tiene coordenadas, y las suyas van de 0 a
    /// 188.
    /// </summary>
    private static readonly TimeSpan CoverTurn = TimeSpan.FromSeconds(40);

    private readonly RotateTransform _coverSpin = new() { CenterX = 94, CenterY = 94 };
    private bool _coverSpinning;

    public DashboardView()
    {
        InitializeComponent();
        CalendarDayItems.ItemsSource = _calendarCells;

        DashboardTabs.SetTabs(
        [
            new DashboardTabDefinition("Panel", FindResource("IconTabPanel") as Geometry),
            new DashboardTabDefinition("Media", FindResource("IconTabMedia") as Geometry),
            new DashboardTabDefinition("Rendimiento", FindResource("IconTabPerformance") as Geometry)
        ]);
        DashboardTabs.SelectionChanged += (_, index) => ShowTab((DashboardTab)index, animate: true);

        // Diseno D49 - las dos caratulas se recortan con el contorno de flor. La forma se pide por
        // tamano: a 188 ondula, a 46 la formula la devuelve como circulo, porque una ondulacion de
        // dos pixeles y medio no se lee como borde vivo, solo ensucia el canto.
        var largeCover = PetalGeometry.Create(188);
        MediaCoverBackdrop.Data = largeCover;

        // El fondo gira como elemento; la imagen no gira, gira su recorte. El resultado en
        // pantalla es el mismo contorno en los dos, que es lo que hace falta para que no se vea
        // doble.
        MediaCoverBackdrop.RenderTransform = _coverSpin;

        // La geometría cacheada está congelada —se comparte entre las dos carátulas— y a una
        // congelada no se le puede poner transformación. El clon es de esta máscara y solo de ella.
        var mask = largeCover.Clone();
        mask.Transform = _coverSpin;
        MediaCoverShape.Clip = mask;

        var smallCover = PetalGeometry.Create(46);
        PanelMediaBackdrop.Data = smallCover;
        PanelMediaCoverShape.Data = smallCover;

        BuildCalendarHeader();
        RefreshCalendar();
        RefreshClock();
        RefreshMediaUi();
    }

    public event EventHandler? MediaPlayPauseRequested;

    public event EventHandler? MediaNextRequested;

    public event EventHandler? MediaPreviousRequested;

    /// <summary>
    /// Diseño D53 — pulsar un resumen abre Kohana en ese módulo. El cajón no navega por su cuenta:
    /// avisa, y quien tiene la ventana principal decide. Si el cajón supiera navegar tendría que
    /// conocer el rail, y entonces dejaría de ser una pieza que se puede sacar y poner.
    /// </summary>
    public event EventHandler<string>? ModuleRequested;

    public DashboardTab ActiveTab => _activeTab;

    /// <summary>
    /// Elige la pestaña con la que abrir el cajón. La regla vive en
    /// <see cref="DashboardTabPolicy"/> y no aquí: es una decisión de producto —no enseñar un
    /// reproductor vacío— y se puede probar sin abrir una ventana.
    /// </summary>
    public void PrepareForReveal()
    {
        var tab = DashboardTabPolicy.Resolve(_activeTab, _media.HasSession);
        DashboardTabs.Select((int)tab);
        ShowTab(tab, animate: false);
        RefreshClock();
    }

    /// <summary>
    /// Diseño D58 — cuánto se desplaza la pestaña que entra.
    ///
    /// Más que el desplazamiento vertical anterior (12) porque ahora el recorrido tiene que leerse
    /// como una dirección y no solo como un empujón: con doce píxeles en horizontal no se distingue
    /// venir de la derecha de venir de la izquierda.
    /// </summary>
    private const double TabEnterOffset = 32;

    private void ShowTab(DashboardTab tab, bool animate)
    {
        // La dirección se calcula antes de mover _activeTab, que es de dónde venimos.
        var direction = DashboardTabPolicy.EnterDirection(_activeTab, tab);
        _activeTab = tab;

        // Se entra desde el lado contrario al que se va: al avanzar a la derecha, la página nueva
        // llega desde la derecha, igual que la hoja de un libro.
        var offset = direction * TabEnterOffset;

        Show(PanelPane, PanelPaneTranslate, tab == DashboardTab.Panel, animate, offset);
        Show(MediaPane, MediaPaneTranslate, tab == DashboardTab.Media, animate, offset);
        Show(PerformancePane, PerformancePaneTranslate, tab == DashboardTab.Performance, animate, offset);

        static void Show(
            UIElement pane,
            TranslateTransform transform,
            bool visible,
            bool animate,
            double offset)
        {
            if (!visible)
            {
                pane.Visibility = Visibility.Collapsed;
                return;
            }

            pane.Visibility = Visibility.Visible;

            // Sin dirección —volver a pulsar la pestaña activa, o abrir el cajón de cero— no hay
            // hacia dónde ir, así que se conserva el gesto vertical de antes.
            if (animate && offset != 0)
            {
                pane.EnterFrom(transform, fromOffset: offset);
            }
            else if (animate)
            {
                pane.EnterFrom(transform, fromOffset: 12, vertical: true);
            }
            else
            {
                pane.Opacity = 1;
                transform.X = 0;
                transform.Y = 0;
            }
        }
    }

    public void RefreshClock()
    {
        var now = DateTime.Now;

        PanelClockHourText.Text = now.ToString("hh", CultureInfo.CurrentCulture);
        PanelClockMinuteText.Text = now.ToString("mm", CultureInfo.CurrentCulture);

        // En español Windows escribe el meridiano como «p. m.»; en mayúsculas queda «P. M.», que
        // bajo un reloj de dos cifras se lee como una abreviatura rara. Se dejan solo las letras.
        PanelClockMeridiemText.Text = new string(now
            .ToString("tt", CultureInfo.CurrentCulture)
            .ToUpperInvariant()
            .Where(char.IsLetter)
            .ToArray());

        var today = DateOnly.FromDateTime(now);
        if (_calendarCells.Count > 0 && !_calendarCells.Any(cell => cell.IsToday && cell.Date == today))
        {
            RefreshCalendar();
        }
    }

    public void UpdateSession(string? profileName, TimeSpan? uptime)
    {
        PanelSessionTitleText.Text = string.IsNullOrWhiteSpace(profileName) ? "Kohana" : profileName;
        PanelUptimeText.Text = uptime is { } value
            ? $"Equipo encendido {DescribeUptime(value)}"
            : "Equipo encendido";
    }

    private static string DescribeUptime(TimeSpan uptime)
    {
        if (uptime.TotalMinutes < 1)
        {
            return "hace menos de un minuto";
        }

        if (uptime.TotalHours < 1)
        {
            var minutes = (int)uptime.TotalMinutes;
            return minutes == 1 ? "hace 1 minuto" : $"hace {minutes} minutos";
        }

        if (uptime.TotalDays < 1)
        {
            var hours = (int)uptime.TotalHours;
            var minutes = uptime.Minutes;
            var hourText = hours == 1 ? "1 hora" : $"{hours} horas";
            return minutes == 0 ? $"hace {hourText}" : $"hace {hourText} y {minutes} min";
        }

        var days = (int)uptime.TotalDays;
        return days == 1 ? "hace 1 día" : $"hace {days} días";
    }

    /// <summary>
    /// Diseño D53 — el resumen del día, que antes vivía en Inicio.
    ///
    /// Los tres valores llegan ya formateados desde el resumen del día, así que aquí no se
    /// reinterpretan: lo único que se decide es cuándo un dato no merece enseñarse. La insignia de
    /// pendientes sigue la misma regla que en Inicio —sin nada pendiente, no hay insignia— y los
    /// otros dos ocultan su valor cuando no hay nada que contar, en vez de enseñar un guion.
    /// </summary>
    public void UpdateDailySummary(
        string? taskValue,
        string? taskDetail,
        string? focusValue,
        string? focusDetail,
        string? routineValue,
        string? routineDetail)
    {
        var tasks = taskValue?.Trim() ?? string.Empty;
        var nothingPending = tasks.Length == 0 || (int.TryParse(tasks, out var count) && count == 0);

        SummaryTasksCount.Text = tasks;
        SummaryTasksBadge.Visibility = nothingPending ? Visibility.Collapsed : Visibility.Visible;
        SummaryTasksDetail.Text = Fallback(taskDetail, "Nada urgente por ahora");

        SummaryFocusValue.Text = Meaningful(focusValue) ? focusValue!.Trim() : string.Empty;
        SummaryFocusDetail.Text = Fallback(focusDetail, "Sin sesión activa");

        SummaryRoutinesValue.Text = Meaningful(routineValue) ? routineValue!.Trim() : string.Empty;
        SummaryRoutinesDetail.Text = Fallback(routineDetail, "Ninguna preparada");

        // Un guion es lo que se enseña cuando no se sabe; aquí sí se sabe, y lo que se sabe es que
        // no hay nada. Eso se dice con palabras.
        static bool Meaningful(string? value) =>
            value is not null &&
            value.Trim().Length > 0 &&
            value.Trim() is not ("—" or "-" or "0");

        static string Fallback(string? value, string whenEmpty) =>
            string.IsNullOrWhiteSpace(value) ? whenEmpty : value.Trim();
    }

    private void SummaryTasksButton_Click(object sender, RoutedEventArgs e) =>
        ModuleRequested?.Invoke(this, "Tasks");

    private void SummaryFocusButton_Click(object sender, RoutedEventArgs e) =>
        ModuleRequested?.Invoke(this, "Focus");

    private void SummaryRoutinesButton_Click(object sender, RoutedEventArgs e) =>
        ModuleRequested?.Invoke(this, "Routines");

    // ---------- Calendario ----------

    private void BuildCalendarHeader()
    {
        var culture = CultureInfo.CurrentCulture;
        CalendarWeekdayItems.ItemsSource = MonthGrid
            .WeekdayOrder(culture.DateTimeFormat.FirstDayOfWeek)
            .Select(day =>
            {
                var name = culture.DateTimeFormat.GetShortestDayName(day);
                return name.Length > 0 ? char.ToUpper(name[0], culture) + name[1..] : name;
            })
            .ToArray();
    }

    private void RefreshCalendar()
    {
        var culture = CultureInfo.CurrentCulture;
        var today = DateOnly.FromDateTime(DateTime.Now);

        var monthName = culture.DateTimeFormat.GetMonthName(_calendarMonth.Month);
        CalendarMonthText.Text =
            $"{char.ToUpper(monthName[0], culture)}{monthName[1..]} {_calendarMonth.Year}";

        var inMonth = (Brush)FindResource("BrushTextPrimary");
        var outMonth = (Brush)FindResource("BrushTextTertiary");
        var todayBackground = (Brush)FindResource("BrushAccent");

        _calendarCells.Clear();
        foreach (var day in MonthGrid.Build(_calendarMonth, today, culture.DateTimeFormat.FirstDayOfWeek))
        {
            _calendarCells.Add(new CalendarCell(
                Date: day.Date,
                Day: day.Date.Day.ToString(CultureInfo.CurrentCulture),
                IsToday: day.IsToday,
                Foreground: day.IsToday ? Brushes.White : day.InMonth ? inMonth : outMonth,
                Background: day.IsToday ? todayBackground : Brushes.Transparent,
                Weight: day.IsToday || day.InMonth ? FontWeights.SemiBold : FontWeights.Normal));
        }
    }

    private void CalendarPreviousButton_Click(object sender, RoutedEventArgs e)
    {
        _calendarMonth = _calendarMonth.AddMonths(-1);
        RefreshCalendar();
    }

    private void CalendarNextButton_Click(object sender, RoutedEventArgs e)
    {
        _calendarMonth = _calendarMonth.AddMonths(1);
        RefreshCalendar();
    }

    private sealed record CalendarCell(
        DateOnly Date,
        string Day,
        bool IsToday,
        Brush Foreground,
        Brush Background,
        FontWeight Weight);

    // ---------- Métricas ----------

    public void UpdateSnapshot(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        CpuCapsule.Percent = snapshot.CpuUsagePercent;
        CpuCapsule.Detail = "Uso ahora mismo";

        GpuCapsule.Percent = snapshot.GpuUsagePercent;
        GpuCapsule.Detail = snapshot.DedicatedGpuMemoryBytes.HasValue
            ? $"VRAM: {FormatBytes(snapshot.DedicatedGpuMemoryBytes.Value)}"
            : "VRAM no disponible";

        MemoryCapsule.Percent = snapshot.MemoryUsagePercent;
        MemoryCapsule.Detail = snapshot.TotalMemoryBytes > 0
            ? $"{FormatBytes((long)snapshot.UsedMemoryBytes)}{Environment.NewLine}de {FormatBytes((long)snapshot.TotalMemoryBytes)}"
            : "En uso";

        DiskCapsule.Percent = snapshot.SystemDriveUsagePercent;

        // Los mismos tres datos, resumidos en el panel. Salen de esta misma lectura y no de otra
        // propia: dos consultas separadas por medio segundo se contradicen entre sí, y ver el 40%
        // arriba y el 43% abajo en la misma ventana hace dudar de las dos.
        PanelCpuRing.Percent = snapshot.CpuUsagePercent;
        PanelMemoryRing.Percent = snapshot.MemoryUsagePercent;
        PanelDiskRing.Percent = snapshot.SystemDriveUsagePercent;
    }

    public void UpdateHardwareNames(string? processorName, string? graphicsName)
    {
        CpuCapsule.Subtitle = string.IsNullOrWhiteSpace(processorName) ? "Procesador" : processorName;
        GpuCapsule.Subtitle = string.IsNullOrWhiteSpace(graphicsName) ? "Gráfica" : graphicsName;
    }

    public void UpdateThroughput(
        double downloadBytesPerSecond,
        double uploadBytesPerSecond,
        long? driveUsedBytes,
        long? driveTotalBytes,
        string? driveName)
    {
        NetworkCapsule.Subtitle = "Ahora mismo";
        NetworkCapsule.Detail =
            $"↓ {FormatRate(downloadBytesPerSecond)}{Environment.NewLine}↑ {FormatRate(uploadBytesPerSecond)}";

        // El medidor de la red se queda vacío a propósito: un porcentaje necesita un máximo, y la
        // velocidad de una conexión no lo tiene.
        NetworkCapsule.Percent = null;

        if (driveUsedBytes is { } used && driveTotalBytes is { } total && total > 0)
        {
            DiskCapsule.Subtitle = string.IsNullOrWhiteSpace(driveName) ? "Unidad del sistema" : driveName;
            DiskCapsule.Detail = $"{FormatBytes(used)}{Environment.NewLine}de {FormatBytes(total)}";
        }
        else
        {
            DiskCapsule.Subtitle = "Unidad del sistema";
            DiskCapsule.Detail = "Espacio no disponible";
        }
    }

    // ---------- Media ----------

    public void UpdateMedia(MediaSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var trackChanged = !snapshot.SameTrackAs(_media);
        _media = snapshot;

        if (trackChanged)
        {
            var cover = CreateCover(snapshot.Thumbnail);
            SetCover(MediaCoverShape, MediaCoverBrush, cover);
            SetCover(PanelMediaCoverShape, PanelMediaCoverBrush, cover);
            PanelMediaPlaceholderIcon.Visibility = cover is null ? Visibility.Visible : Visibility.Collapsed;
        }

        RefreshMediaUi();
    }

    public bool HasMediaSession => _media.HasSession;

    /// <summary>
    /// Cierto solo cuando el anillo y la onda tienen algo que decir: la pestaña Media a la vista y
    /// música sonando. Quien mueve los fotogramas lo consulta para no gastar treinta repintados por
    /// segundo dibujando el silencio detrás de otra pestaña.
    /// </summary>
    public bool WantsAnimationFrames =>
        _activeTab == DashboardTab.Media && _media.HasSession && _media.IsPlaying;

    /// <summary>Avanza un fotograma del anillo, de la onda y del tiempo transcurrido.</summary>
    public void RenderAudioFrame(IReadOnlyList<double> spectrumLevels, double wavePhase)
    {
        ArgumentNullException.ThrowIfNull(spectrumLevels);

        CoverRing.SetLevels(spectrumLevels);
        MediaProgressBar.Phase = wavePhase;
        RefreshMediaPosition();
    }

    /// <summary>
    /// Diseño D48 — el tiempo y la barra, recalculados desde el reloj.
    ///
    /// Se hace en cada fotograma y no solo cuando llega una lectura nueva porque el reproductor
    /// informa su posición cada cinco o seis segundos: pintar solo con esas lecturas es lo que
    /// dejaba la barra clavada y luego pegando un salto. La lectura sigue siendo la única fuente
    /// de verdad; lo que se hace aquí es contar el tiempo que ha pasado desde ella.
    /// </summary>
    private void RefreshMediaPosition()
    {
        var position = MediaProgress.Resolve(
            _media.Position,
            _media.PositionUpdatedAt,
            DateTimeOffset.UtcNow,
            _media.IsPlaying,
            _media.Duration);

        MediaPositionText.Text = MediaProgress.Format(position);

        var fraction = MediaProgress.Fraction(position, _media.Duration);
        MediaProgressBar.Progress = fraction ?? 0;
        MediaProgressBar.IsWaving = _media.IsPlaying;
    }

    private void RefreshMediaUi()
    {
        var playing = _media.HasSession;

        MediaEmptyPanel.Visibility = playing ? Visibility.Collapsed : Visibility.Visible;
        MediaNowPlayingPanel.Visibility = playing ? Visibility.Visible : Visibility.Collapsed;

        var playIcon = (Geometry)FindResource(_media.IsPlaying ? "IconMediaPause" : "IconMediaPlay");
        MediaPlayPauseIcon.Data = playIcon;
        PanelMediaPlayIcon.Data = playIcon;

        ApplyCoverSpin(playing && _media.IsPlaying);

        PanelMediaPlayButton.IsEnabled = playing && _media.CanPlayPause;
        MediaPlayPauseButton.IsEnabled = _media.CanPlayPause;
        MediaNextButton.IsEnabled = _media.CanGoNext;
        MediaPreviousButton.IsEnabled = _media.CanGoPrevious;

        if (!playing)
        {
            PanelMediaTitleText.Text = "Nada sonando";
            PanelMediaArtistText.Text = "Sin reproducción";
            return;
        }

        MediaTitleText.Text = _media.Title.Length > 0 ? _media.Title : "Sin título";
        MediaArtistText.Text = _media.Artist.Length > 0 ? _media.Artist : "Artista desconocido";
        MediaAlbumText.Text = _media.Album;
        MediaAlbumText.Visibility = _media.Album.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        MediaSourceText.Text = _media.SourceApp.Length > 0 ? _media.SourceApp : "Windows";

        PanelMediaTitleText.Text = MediaTitleText.Text;
        PanelMediaArtistText.Text = MediaArtistText.Text;

        MediaDurationText.Text = MediaProgress.Format(_media.Duration);

        // Una emisión en directo no tiene final: enseñar «0:00» a la derecha haría pensar que la
        // duración no se pudo leer, cuando lo cierto es que no existe.
        MediaDurationText.Visibility = _media.Duration is { } total && total > TimeSpan.Zero
            ? Visibility.Visible
            : Visibility.Collapsed;

        RefreshMediaPosition();
    }

    /// <summary>
    /// Arranca o detiene el giro. Al detenerse se conserva el ángulo en el que iba: reiniciarlo a
    /// cero haría que la carátula pegara un salto al pausar, que es justo el momento en el que uno
    /// está mirándola.
    /// </summary>
    private void ApplyCoverSpin(bool spinning)
    {
        if (spinning == _coverSpinning)
        {
            return;
        }

        _coverSpinning = spinning;

        var angle = _coverSpin.Angle;
        _coverSpin.BeginAnimation(RotateTransform.AngleProperty, null);
        _coverSpin.Angle = angle;

        if (!spinning || !KohanaMotion.AnimationsEnabled)
        {
            return;
        }

        var animation = new DoubleAnimation
        {
            From = angle,
            To = angle + 360,
            Duration = new Duration(CoverTurn),
            RepeatBehavior = RepeatBehavior.Forever
        };

        _coverSpin.BeginAnimation(RotateTransform.AngleProperty, animation);
    }

    private static BitmapImage? CreateCover(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();

            // Sin OnLoad, WPF deja el flujo abierto y la imagen se cae al liberarlo; DecodePixelWidth
            // evita guardar en memoria una carátula de 1400 píxeles para pintarla a 188.
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            image.DecodePixelWidth = 376;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static void SetCover(Shape target, ImageBrush brush, BitmapImage? cover)
    {
        brush.ImageSource = cover;
        target.Visibility = cover is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void MediaPlayPauseButton_Click(object sender, RoutedEventArgs e) =>
        MediaPlayPauseRequested?.Invoke(this, EventArgs.Empty);

    private void MediaNextButton_Click(object sender, RoutedEventArgs e) =>
        MediaNextRequested?.Invoke(this, EventArgs.Empty);

    private void MediaPreviousButton_Click(object sender, RoutedEventArgs e) =>
        MediaPreviousRequested?.Invoke(this, EventArgs.Empty);

    private static string FormatRate(double bytesPerSecond)
    {
        if (bytesPerSecond <= 0)
        {
            return "0 KB/s";
        }

        return bytesPerSecond >= 1024 * 1024
            ? $"{bytesPerSecond / (1024 * 1024):0.0} MB/s"
            : $"{bytesPerSecond / 1024:0} KB/s";
    }

    private static string FormatBytes(long bytes)
    {
        const double megabyte = 1024d * 1024d;
        const double gigabyte = 1024d * 1024d * 1024d;

        return bytes >= gigabyte ? $"{bytes / gigabyte:0.0} GB" : $"{bytes / megabyte:0} MB";
    }
}
