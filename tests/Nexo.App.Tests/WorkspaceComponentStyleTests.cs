using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Nexo.App.Tests;

/// <summary>
/// Diseño D2 — los componentes compartidos de workspace deben resolverse en WPF real.
///
/// El crash de D1.1 lo causó un estilo cuyo XAML era válido y cuyas claves existían, pero que
/// fallaba al materializarse en tiempo de ejecución. Por eso estos estilos no se comprueban
/// leyendo el XAML: se aplican a controles de verdad, se mutan los recursos de acento igual que
/// hace <c>ApplyAccent</c>, se fuerza el layout y se activa el foco, que es cuando WPF resuelve
/// de verdad cada referencia.
/// </summary>
[Collection(StaWpfCollection.Name)]
public sealed class WorkspaceComponentStyleTests
{
    private readonly StaWpfFixture _fixture;

    public WorkspaceComponentStyleTests(StaWpfFixture fixture) => _fixture = fixture;

    public static TheoryData<string, Type> WorkspaceStyles() => new()
    {
        { "WorkspaceSectionTitleStyle", typeof(TextBlock) },
        { "WorkspaceSectionSubtitleStyle", typeof(TextBlock) },
        { "WorkspaceInteractiveCardStyle", typeof(Button) },
        { "WorkspaceMetricValueStyle", typeof(TextBlock) },
        { "WorkspaceMetricLabelStyle", typeof(TextBlock) },
        { "WorkspaceStatusChipStyle", typeof(Border) },
        { "WorkspaceStatusChipTextStyle", typeof(TextBlock) },
        { "WorkspaceEmptyStateTitleStyle", typeof(TextBlock) },
        { "WorkspaceEmptyStateDetailStyle", typeof(TextBlock) },
        { "WorkspaceErrorStateStyle", typeof(Border) },
        { "WorkspaceErrorStateTextStyle", typeof(TextBlock) },
        { "WorkspaceUnavailableStateStyle", typeof(Border) },
        { "WorkspaceLoadingTextStyle", typeof(TextBlock) },
        { "WorkspaceDividerStyle", typeof(Border) },
        { "WorkspaceSearchBoxStyle", typeof(TextBox) }
    };

    [Theory]
    [MemberData(nameof(WorkspaceStyles))]
    public void WorkspaceStyle_ResolvesAndLaysOut_AfterAccentMutation(string styleKey, Type targetType)
    {
        _fixture.Invoke(() =>
        {
            MutateAccentLikeApplyAccent();

            var style = Assert.IsType<Style>(Application.Current.Resources[styleKey]);
            var element = (FrameworkElement)Activator.CreateInstance(targetType)!;
            element.Style = style;

            using var host = new OffscreenHost();
            host.Window.Content = element;
            host.Window.Show();
            host.Window.UpdateLayout();

            // Si alguna referencia del estilo no se resolviera, el fallo saltaría durante Arrange,
            // igual que ocurrió en producción con D1.1.
            Assert.True(element.ActualWidth >= 0);
        });
    }

    [Fact]
    public void InteractiveCard_ShowsItsFocusRing_WithoutThrowing()
    {
        // La tarjeta pulsable usa BrushFocusRing en su disparador IsKeyboardFocused, que es
        // exactamente el recurso que provocó el crash de D1.1 al materializarse por primera vez.
        _fixture.Invoke(() =>
        {
            MutateAccentLikeApplyAccent();

            var card = new Button
            {
                Style = (Style)Application.Current.Resources["WorkspaceInteractiveCardStyle"],
                Content = "Tarjeta"
            };

            using var host = new OffscreenHost();
            host.Window.Content = card;
            host.Window.Show();
            host.Window.UpdateLayout();

            Keyboard.Focus(card);
            host.Window.UpdateLayout();
        });
    }

    [Fact]
    public void InteractiveCard_IsKeyboardReachable()
    {
        // Se eligió Button en vez de un Border con MouseDown precisamente para esto: una tarjeta
        // pulsable tiene que poder usarse sin ratón.
        _fixture.Invoke(() =>
        {
            var card = new Button
            {
                Style = (Style)Application.Current.Resources["WorkspaceInteractiveCardStyle"],
                Content = "Tarjeta"
            };

            using var host = new OffscreenHost();
            host.Window.Content = card;
            host.Window.Show();
            host.Window.UpdateLayout();

            Assert.True(card.Focusable);
            Assert.True(card.IsTabStop);
        });
    }

    [Fact]
    public void InteractiveCard_DoesNotHideFocusWithANullFocusVisual_WithoutReplacingIt()
    {
        // FocusVisualStyle nulo está permitido solo porque el estilo dibuja su propio anillo de
        // foco con BrushFocusRing. Si algún día se quitara ese disparador, el foco se volvería
        // invisible: esta prueba fija esa dependencia.
        _fixture.Invoke(() =>
        {
            var style = (Style)Application.Current.Resources["WorkspaceInteractiveCardStyle"];
            var template = style.Setters
                .OfType<Setter>()
                .Where(setter => setter.Property == Control.TemplateProperty)
                .Select(setter => setter.Value)
                .OfType<ControlTemplate>()
                .Single();

            var focusTrigger = template.Triggers
                .OfType<Trigger>()
                .SingleOrDefault(trigger => trigger.Property == UIElement.IsKeyboardFocusedProperty);

            Assert.NotNull(focusTrigger);
            Assert.Contains(
                focusTrigger.Setters.OfType<Setter>(),
                setter => setter.Property == Border.BorderBrushProperty);
        });
    }

    private static void MutateAccentLikeApplyAccent()
    {
        Application.Current.Resources["BrushAccent"] = new SolidColorBrush(Colors.HotPink);
        Application.Current.Resources["BrushAccentSoft"] = new SolidColorBrush(Colors.HotPink);
        Application.Current.Resources["BrushAccentBorder"] = new SolidColorBrush(
            Color.FromArgb(112, Colors.HotPink.R, Colors.HotPink.G, Colors.HotPink.B));
    }

    private sealed class OffscreenHost : IDisposable
    {
        public Window Window { get; } = new()
        {
            Width = 240,
            Height = 160,
            Left = -5000,
            Top = -5000,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false
        };

        public void Dispose() => Window.Close();
    }
}
