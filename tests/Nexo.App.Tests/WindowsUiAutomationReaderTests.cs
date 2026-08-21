using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using Nexo.Windows.Vision;

namespace Nexo.App.Tests;

/// <summary>
/// Diseño D5.3 (Fase 2 — Sakura Lens) — prueba <see cref="WindowsUiAutomationReader"/> contra una
/// ventana WPF real (no un doble): confirma que el elemento con nombre conocido aparece en la
/// lectura, con una posición válida. Vive en <c>Nexo.App.Tests</c> (no en
/// <c>Nexo.Windows.Tests</c>) porque necesita levantar una ventana WPF real para tener algo que
/// leer — el propio lector no depende de WPF, solo esta prueba lo necesita para fabricar el caso.
/// </summary>
[Collection(StaWpfCollection.Name)]
public sealed class WindowsUiAutomationReaderTests
{
    private readonly StaWpfFixture _fixture;

    public WindowsUiAutomationReaderTests(StaWpfFixture fixture) => _fixture = fixture;

    [Fact]
    public void Read_WithZeroHandle_ReturnsFailure()
    {
        var reader = new WindowsUiAutomationReader();

        var result = reader.Read(0);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Read_OnARealWindow_FindsTheNamedButtonWithValidBounds()
    {
        _fixture.Invoke(() =>
        {
            using var host = new HiddenWindowHost("Etiqueta de prueba Sakura Lens");
            var handle = new WindowInteropHelper(host.Window).Handle;

            var reader = new WindowsUiAutomationReader();
            var result = reader.Read(handle.ToInt64());

            Assert.True(result.IsSuccess);
            // El árbol de un Button con contenido de texto simple expone tanto el propio botón
            // como su etiqueta interna como elementos separados con el mismo Name — se busca
            // específicamente el de tipo Button, no un único elemento con ese nombre.
            var button = Assert.Single(
                result.Elements,
                element => element.Name == "Etiqueta de prueba Sakura Lens" &&
                    element.ControlType == "ControlType.Button");
            Assert.True(button.Width > 0);
            Assert.True(button.Height > 0);
        });
    }

    private sealed class HiddenWindowHost : IDisposable
    {
        public Window Window { get; }

        public HiddenWindowHost(string buttonName)
        {
            var button = new Button
            {
                Content = buttonName,
                Width = 120,
                Height = 30
            };
            AutomationProperties.SetName(button, buttonName);

            Window = new Window
            {
                Width = 200,
                Height = 100,
                Left = -5000,
                Top = -5000,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                ShowActivated = false,
                Content = button
            };
            Window.Show();
        }

        public void Dispose() => Window.Close();
    }
}
