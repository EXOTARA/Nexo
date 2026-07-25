using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Nexo.App.Tests;

/// <summary>
/// Diseño D2.0 — regresión del defecto de iconos de navegación seleccionados.
///
/// El defecto original no era detectable con análisis de texto: el XAML era válido y los estilos
/// existían. Solo aparecía al RENDERIZAR. Estas pruebas renderizan de verdad cada icono con
/// <see cref="RenderTargetBitmap"/> y miden la tinta resultante, que es la única forma objetiva de
/// distinguir "un icono de línea reconocible" de "un bloque sólido".
///
/// Medición previa a la corrección (relleno + StrokeThickness=0), como referencia histórica:
/// Sistema 95.6 %, Hoy 93.2 %, Asistente 79.7 % de cobertura de tinta, y Personalizar 0 %
/// (invisible). En estado normal todos rondaban 3–12 %.
/// </summary>
[Collection(StaWpfCollection.Name)]
public sealed class NavigationIconRenderingTests
{
    private readonly StaWpfFixture _fixture;

    public NavigationIconRenderingTests(StaWpfFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Por encima de esta cobertura (tinta / área del recuadro delimitador) una geometría ha
    /// dejado de leerse como icono de línea y se ha convertido en una mancha sólida. Los iconos
    /// reales de Kohana miden entre 3 % y 17 % en cualquiera de sus dos estados; el defecto
    /// llegaba a 95.6 %. El umbral deja margen amplio para variantes futuras sin volver a admitir
    /// un bloque.
    /// </summary>
    private const double SolidBlockCoverageThreshold = 0.35d;

    public static TheoryData<string> NavigationIconKeys() =>
    [
        "IconKohanaHome",
        "IconKohanaAssistant",
        "IconKohanaTasks",
        "IconKohanaFocus",
        "IconKohanaRoutines",
        "IconKohanaAudio",
        "IconKohanaCapture",
        "IconKohanaSystem",
        "IconKohanaSettings"
    ];

    /// <summary>
    /// Prueba de regresión central de D2.0. Las tres condiciones son facetas del mismo defecto —
    /// rellenar una geometría de línea — y se comprueban juntas porque por separado no describen
    /// el fallo: el icono debe seguir siendo visible, no convertirse en mancha sólida, y conservar
    /// la silueta del estado normal.
    /// </summary>
    [Theory]
    [MemberData(nameof(NavigationIconKeys))]
    public void SelectedIcon_KeepsLineArtIdentity(string geometryKey)
    {
        _fixture.Invoke(() =>
        {
            var normal = RenderIcon(geometryKey, "SakuraNavigationIconStyle");
            var selected = RenderIcon(geometryKey, "SakuraNavigationIconSelectedStyle");

            // 'Personalizar' son solo segmentos sin área encerrada: al rellenarlo con
            // StrokeThickness=0 desaparecía por completo (0 px medidos).
            Assert.True(
                selected.Ink > 0,
                $"El icono '{geometryKey}' seleccionado no pinta ningún píxel: es invisible.");

            Assert.True(
                selected.Coverage < SolidBlockCoverageThreshold,
                $"El icono '{geometryKey}' seleccionado cubre el {selected.Coverage:P1} de su " +
                $"recuadro: se está renderizando como un bloque sólido en vez de conservar su " +
                $"silueta de línea.");

            // Un trazo más grueso ensancha ligeramente el recuadro, pero la silueta es la misma
            // geometría: unos pocos píxeles de diferencia, no una forma distinta.
            Assert.InRange(selected.Width - normal.Width, 0, 6);
            Assert.InRange(selected.Height - normal.Height, 0, 6);
        });
    }

    [Theory]
    [MemberData(nameof(NavigationIconKeys))]
    public void SelectedIcon_IsHeavierThanNormal_SoStateIsNotColorOnly(string geometryKey)
    {
        // El estado activo nunca debe depender solo del color (principio de Diseño D1). Al dejar
        // de rellenar, el grosor del trazo pasa a ser esa señal independiente del color, así que
        // debe seguir siendo medible.
        _fixture.Invoke(() =>
        {
            var normal = RenderIcon(geometryKey, "SakuraNavigationIconStyle");
            var selected = RenderIcon(geometryKey, "SakuraNavigationIconSelectedStyle");

            Assert.True(
                selected.Ink > normal.Ink,
                $"El icono '{geometryKey}' seleccionado ({selected.Ink} px) no se distingue del " +
                $"normal ({normal.Ink} px) por grosor: el estado activo quedaría dependiendo solo " +
                $"del color.");
        });
    }

    [Theory]
    [MemberData(nameof(NavigationIconKeys))]
    public void Icon_IsNotClipped_InEitherState(string geometryKey)
    {
        _fixture.Invoke(() =>
        {
            foreach (var styleKey in new[] { "SakuraNavigationIconStyle", "SakuraNavigationIconSelectedStyle" })
            {
                var rendered = RenderIcon(geometryKey, styleKey);

                // Si la geometría tocara los bordes del lienzo estaría recortada. Se renderiza en
                // un lienzo mayor que el icono, así que debe quedar holgura por los cuatro lados.
                Assert.True(rendered.MinX > 0, $"'{geometryKey}' ({styleKey}) recortado por la izquierda.");
                Assert.True(rendered.MinY > 0, $"'{geometryKey}' ({styleKey}) recortado por arriba.");
                Assert.True(rendered.MaxX < CanvasSize - 1, $"'{geometryKey}' ({styleKey}) recortado por la derecha.");
                Assert.True(rendered.MaxY < CanvasSize - 1, $"'{geometryKey}' ({styleKey}) recortado por abajo.");
            }
        });
    }

    [Fact]
    public void SelectedIconStyle_DoesNotFillTheGeometry()
    {
        // Comprobación directa de la causa raíz: rellenar geometrías de línea fue el defecto.
        _fixture.Invoke(() =>
        {
            var style = (Style)Application.Current.Resources["SakuraNavigationIconSelectedStyle"];
            var fillSetter = style.Setters
                .OfType<Setter>()
                .FirstOrDefault(setter => setter.Property == System.Windows.Shapes.Path.FillProperty);

            Assert.True(
                fillSetter is null,
                "SakuraNavigationIconSelectedStyle vuelve a establecer Fill: eso convierte los " +
                "iconos de línea en bloques sólidos (defecto D2.0).");
        });
    }

    [Fact]
    public void SelectedIconStyle_KeepsStrokeVisible()
    {
        _fixture.Invoke(() =>
        {
            var style = (Style)Application.Current.Resources["SakuraNavigationIconSelectedStyle"];
            var host = new Button { Foreground = Brushes.White };
            var path = new System.Windows.Shapes.Path
            {
                Data = (Geometry)Application.Current.Resources["IconKohanaSettings"],
                Style = style
            };
            host.Content = path;
            host.Measure(new Size(CanvasSize, CanvasSize));

            Assert.True(
                path.StrokeThickness > 0,
                "El icono seleccionado tiene StrokeThickness=0: las geometrías de línea " +
                "desaparecen por completo (defecto D2.0).");
        });
    }

    [Fact]
    public void EveryNavigationIcon_KeepsTheSameRenderedSize()
    {
        // El icono seleccionado no debe cambiar el tamaño del botón ni desplazar el texto: ambas
        // variantes comparten Width/Height del mismo token.
        _fixture.Invoke(() =>
        {
            var normal = (Style)Application.Current.Resources["SakuraNavigationIconStyle"];
            var selected = (Style)Application.Current.Resources["SakuraNavigationIconSelectedStyle"];

            Assert.Equal(ReadDoubleSetter(normal, FrameworkElement.WidthProperty), ReadDoubleSetter(selected, FrameworkElement.WidthProperty));
            Assert.Equal(ReadDoubleSetter(normal, FrameworkElement.HeightProperty), ReadDoubleSetter(selected, FrameworkElement.HeightProperty));
        });
    }

    private const int CanvasSize = 96;

    private static double? ReadDoubleSetter(Style style, DependencyProperty property)
    {
        // El valor puede llegar como DynamicResourceExtension (aún sin resolver) o ya resuelto;
        // basta con que ambas variantes declaren exactamente lo mismo.
        var setter = style.Setters.OfType<Setter>().FirstOrDefault(s => s.Property == property);
        if (setter is null && style.BasedOn is not null)
        {
            return ReadDoubleSetter(style.BasedOn, property);
        }

        return setter?.Value switch
        {
            double value => value,
            _ => null
        };
    }

    private static RenderedIcon RenderIcon(string geometryKey, string styleKey)
    {
        var path = new System.Windows.Shapes.Path
        {
            Data = (Geometry)Application.Current.Resources[geometryKey],
            Style = (Style)Application.Current.Resources[styleKey],
            Width = CanvasSize * 0.7,
            Height = CanvasSize * 0.7,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Los estilos enlazan Stroke al Foreground del Button ancestro, igual que en el shell real.
        var host = new Button
        {
            Foreground = Brushes.White,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Content = path
        };

        var root = new Border
        {
            Width = CanvasSize,
            Height = CanvasSize,
            Background = Brushes.Black,
            Child = host
        };

        root.Measure(new Size(CanvasSize, CanvasSize));
        root.Arrange(new Rect(0, 0, CanvasSize, CanvasSize));
        root.UpdateLayout();

        var bitmap = new RenderTargetBitmap(CanvasSize, CanvasSize, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);

        var pixels = new byte[CanvasSize * CanvasSize * 4];
        bitmap.CopyPixels(pixels, CanvasSize * 4, 0);

        var ink = 0;
        int minX = CanvasSize, minY = CanvasSize, maxX = -1, maxY = -1;
        for (var y = 0; y < CanvasSize; y++)
        {
            for (var x = 0; x < CanvasSize; x++)
            {
                var offset = ((y * CanvasSize) + x) * 4;
                var luminance = Math.Max(pixels[offset], Math.Max(pixels[offset + 1], pixels[offset + 2]));
                if (luminance <= 40)
                {
                    continue;
                }

                ink++;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        return new RenderedIcon(ink, minX, minY, maxX, maxY);
    }

    private readonly record struct RenderedIcon(int Ink, int MinX, int MinY, int MaxX, int MaxY)
    {
        public int Width => MaxX - MinX + 1;

        public int Height => MaxY - MinY + 1;

        public double Coverage => Ink == 0 ? 0d : (double)Ink / (Width * Height);
    }
}
