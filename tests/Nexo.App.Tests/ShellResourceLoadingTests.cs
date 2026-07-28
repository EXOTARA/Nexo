using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Nexo.App.Views;
using Nexo.Core.Audio;
using Nexo.Core.Automation;
using Nexo.Core.Focus;
using Nexo.Core.Tasks;

namespace Nexo.App.Tests;

/// <summary>
/// Diseño D1.1 (hotfix): reproduce en WPF real, no en texto, la clase de error que
/// <c>SakuraShellStructureTests</c> no puede detectar — resolución de recursos en tiempo de
/// ejecución. El crash original ocurría porque <c>ApplyAccent</c> sobrescribe
/// <c>Application.Current.Resources</c> directamente, y una referencia <c>StaticResource</c>
/// entre archivos de tema distintos (<c>BrushFocusRing</c> en Brushes.xaml → <c>ColorAccentBorder</c>
/// en Colors.xaml) fallaba al materializarse por primera vez después de esa escritura, durante el
/// primer layout de un elemento recién insertado — exactamente lo que pasa al navegar.
///
/// No se instancia <see cref="MainWindow"/> completa: su constructor toca el archivo real de
/// preferencias del usuario (<c>JsonSettingsStore</c> sin ruta inyectable) y construiría un grafo
/// de dependencias de producción solo para un hotfix visual. En su lugar, esta clase ejercita el
/// mecanismo compartido real (aplicación de tema, mutación de recursos, layout de un control con
/// los estilos Sakura, e inserción de cada vista real en un host) sin mover lógica de negocio.
/// </summary>
[Collection(StaWpfCollection.Name)]
public sealed class ShellResourceLoadingTests
{
    private readonly StaWpfFixture _fixture;

    public ShellResourceLoadingTests(StaWpfFixture fixture) => _fixture = fixture;

    [Fact]
    public void FocusRing_ResolvesAfterAccentResourceMutation_WithoutThrowing()
    {
        _fixture.Invoke(() =>
        {
            ApplySameMutationPatternAsApplyAccent();

            using var host = new HiddenWindowHost();
            var button = new Button
            {
                Style = (Style)Application.Current.Resources["SakuraNavigationItemStyle"],
                Content = "Prueba"
            };
            host.Window.Content = button;
            host.Window.Show();
            host.Window.UpdateLayout();

            // El disparador IsKeyboardFocused de SakuraNavigationItemStyle es el consumidor real
            // de BrushFocusRing: forzar el foco es lo que materializa la referencia por primera
            // vez, igual que ocurre en producción al enfocar un botón de navegación.
            Keyboard.Focus(button);
            host.Window.UpdateLayout();
        });
    }

    [Fact]
    public void SidebarToggleFocusRing_ResolvesAfterAccentResourceMutation_WithoutThrowing()
    {
        _fixture.Invoke(() =>
        {
            ApplySameMutationPatternAsApplyAccent();

            using var host = new HiddenWindowHost();
            var button = new Button
            {
                Style = (Style)Application.Current.Resources["SakuraSidebarToggleStyle"],
                Content = "Prueba"
            };
            host.Window.Content = button;
            host.Window.Show();
            host.Window.UpdateLayout();

            Keyboard.Focus(button);
            host.Window.UpdateLayout();
        });
    }

    [Theory]
    [InlineData(typeof(HomeView))]
    [InlineData(typeof(AssistantView))]
    [InlineData(typeof(TasksView))]
    [InlineData(typeof(FocusView))]
    [InlineData(typeof(RoutinesView))]
    [InlineData(typeof(AudioView))]
    [InlineData(typeof(CaptureView))]
    [InlineData(typeof(SystemView))]
    [InlineData(typeof(SettingsView))]
    public void EveryShellView_LoadsAndLaysOutAfterAccentMutation_WithoutThrowing(Type viewType)
    {
        // Esto reproduce el mecanismo exacto del crash reportado: ModuleHost.Content = view
        // inserta una UserControl recién creada en el árbol visual, y su primer Arrange es donde
        // se materializan por primera vez las referencias DynamicResource de esa vista. Se prueban
        // las nueve vistas reales, no una sola, porque el reporte confirmó que el fallo ocurre
        // "con cualquier destino distinto de Inicio".
        _fixture.Invoke(() =>
        {
            ApplySameMutationPatternAsApplyAccent();

            using var host = new HiddenWindowHost();
            var view = CreateView(viewType);
            host.Window.Content = view;
            host.Window.Show();
            host.Window.UpdateLayout();
        });
    }

    private static void ApplySameMutationPatternAsApplyAccent()
    {
        // Mismo patrón que MainWindow.ApplyAccent(): escritura directa sobre
        // Application.Current.Resources, ANTES de que nada haya consumido BrushFocusRing todavía
        // — el mismo orden temporal que dispara el bug en producción (ApplyAccent corre en el
        // constructor de MainWindow, antes de que el usuario navegue o enfoque algo).
        Application.Current.Resources["BrushAccent"] = new SolidColorBrush(Colors.HotPink);
        Application.Current.Resources["BrushAccentSoft"] = new SolidColorBrush(Colors.HotPink);
        Application.Current.Resources["BrushAccentBorder"] = new SolidColorBrush(
            Color.FromArgb(112, Colors.HotPink.R, Colors.HotPink.G, Colors.HotPink.B));
    }

    private static UserControl CreateView(Type viewType)
    {
        if (viewType == typeof(TasksView))
        {
            return new TasksView(new TaskManager(new FakeTaskStore()));
        }

        if (viewType == typeof(FocusView))
        {
            return new FocusView(new Nexo.Core.Focus.FocusManager(new FakeFocusStore()));
        }

        if (viewType == typeof(RoutinesView))
        {
            return new RoutinesView(new RoutineManager(new FakeRoutineStore()));
        }

        if (viewType == typeof(AudioView))
        {
            return new AudioView(new FakeAudioMixerService());
        }

        return (UserControl)Activator.CreateInstance(viewType)!;
    }

    private sealed class HiddenWindowHost : IDisposable
    {
        public Window Window { get; } = new()
        {
            Width = 60,
            Height = 60,
            Left = -5000,
            Top = -5000,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent
        };

        public void Dispose() => Window.Close();
    }

    private sealed class FakeTaskStore : ITaskStore
    {
        public IReadOnlyList<Nexo.Core.Tasks.NexoTask> Load() => [];

        public void Save(IReadOnlyCollection<Nexo.Core.Tasks.NexoTask> tasks)
        {
        }
    }

    private sealed class FakeFocusStore : IFocusStore
    {
        public FocusState Load() => new();

        public void Save(FocusState state)
        {
        }
    }

    private sealed class FakeRoutineStore : IRoutineStore
    {
        public RoutineState Load() => new();

        public void Save(RoutineState state)
        {
        }
    }

    private sealed class FakeAudioMixerService : IAudioMixerService
    {
        // AudioView.xaml.cs suscribe Loaded a un manejador async void que llama RefreshAsync(),
        // el cual invoca ReadSnapshot() automáticamente al insertar la vista en el árbol visual
        // (el mismo momento que se está probando aquí). Una excepción sin capturar dentro de un
        // async void es fatal para el hilo — por eso ReadSnapshot() debe devolver un resultado
        // válido en vez de lanzar, a diferencia de los demás métodos, que solo se invocan desde
        // gestos de usuario que estas pruebas no disparan.
        public AudioMixerSnapshot ReadSnapshot() =>
            AudioMixerSnapshot.Unavailable("Dispositivo de audio no disponible en la prueba.");

        public AudioActionResult SetMasterVolume(double percent) => throw new NotSupportedException();

        public AudioActionResult SetMasterMuted(bool muted) => throw new NotSupportedException();

        public AudioActionResult SetSessionVolume(string sessionId, double percent) => throw new NotSupportedException();

        public AudioActionResult SetSessionMuted(string sessionId, bool muted) => throw new NotSupportedException();

        public AudioActionResult SetApplicationVolume(string target, double percent) => throw new NotSupportedException();

        public AudioActionResult ScaleApplicationVolume(string target, double factor) => throw new NotSupportedException();

        public AudioActionResult ChangeApplicationVolume(string target, double deltaPoints) => throw new NotSupportedException();

        public AudioActionResult SetApplicationMuted(string target, bool muted) => throw new NotSupportedException();

        public AudioActionResult LowerAllExcept(string target, double factor) => throw new NotSupportedException();
    }
}
