using Nexo.Core.Metrics;
using Nexo.Core.Resources;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D58 — el modo Ocupado tiene que sostenerse antes de cambiar nada.
///
/// Estas pruebas existen por un defecto que Adler vio usando la aplicación: Kohana anunció «El
/// equipo está ocupado: GPU 100%» sin nada pesado abierto y sin haberle pedido nada a la IA. Una
/// sola lectura bastaba, y los contadores de GPU de Windows dan picos por muestra.
/// </summary>
public sealed class ResourceModeStabilizerTests
{
    private static ResourceGovernorDecision Busy() =>
        new(
            ResourceMode.Busy,
            "El equipo está ocupado: GPU 100%.",
            AllowLocalCommands: true,
            AllowLocalAi: false,
            AllowRemoteAi: true,
            AllowVision: false,
            PauseWakeWord: false,
            SuppressTransientOverlays: false);

    private static ResourceGovernorDecision Game() =>
        new(
            ResourceMode.Game,
            "Un juego está usando la pantalla completa.",
            AllowLocalCommands: true,
            AllowLocalAi: false,
            AllowRemoteAi: false,
            AllowVision: false,
            PauseWakeWord: true,
            SuppressTransientOverlays: true);

    [Fact]
    public void ASingleSpike_DoesNotChangeTheMode()
    {
        // El defecto entero: un golpe de decodificación de vídeo cruza el umbral una muestra.
        var stabilizer = new ResourceModeStabilizer();
        stabilizer.Stabilize(ResourceGovernorDecision.Normal);

        Assert.Equal(ResourceMode.Normal, stabilizer.Stabilize(Busy()).Mode);
    }

    [Fact]
    public void SustainedLoad_EventuallyBecomesBusy()
    {
        var stabilizer = new ResourceModeStabilizer(confirmations: 3);
        stabilizer.Stabilize(ResourceGovernorDecision.Normal);

        Assert.Equal(ResourceMode.Normal, stabilizer.Stabilize(Busy()).Mode);
        Assert.Equal(ResourceMode.Normal, stabilizer.Stabilize(Busy()).Mode);
        Assert.Equal(ResourceMode.Busy, stabilizer.Stabilize(Busy()).Mode);
    }

    [Fact]
    public void ASpikeThatDrops_ResetsTheCount()
    {
        // Dos altas, una baja, dos altas no son cinco seguidas: no debe entrar en Ocupado.
        var stabilizer = new ResourceModeStabilizer(confirmations: 3);
        stabilizer.Stabilize(ResourceGovernorDecision.Normal);

        stabilizer.Stabilize(Busy());
        stabilizer.Stabilize(Busy());
        Assert.Equal(ResourceMode.Normal, stabilizer.Stabilize(ResourceGovernorDecision.Normal).Mode);
        stabilizer.Stabilize(Busy());
        Assert.Equal(ResourceMode.Normal, stabilizer.Stabilize(Busy()).Mode);
    }

    [Fact]
    public void LeavingBusy_AlsoNeedsConfirming()
    {
        // Confirmar solo la entrada dejaría el modo parpadeando con una carga que ronda el umbral,
        // que molesta más que el fallo original.
        var stabilizer = new ResourceModeStabilizer(confirmations: 2);
        stabilizer.Stabilize(ResourceGovernorDecision.Normal);
        stabilizer.Stabilize(Busy());
        Assert.Equal(ResourceMode.Busy, stabilizer.Stabilize(Busy()).Mode);

        Assert.Equal(ResourceMode.Busy, stabilizer.Stabilize(ResourceGovernorDecision.Normal).Mode);
        Assert.Equal(ResourceMode.Normal, stabilizer.Stabilize(ResourceGovernorDecision.Normal).Mode);
    }

    [Fact]
    public void FullScreen_IsNeverDelayed()
    {
        // Esperar confirmaciones aquí es justo la ventana en la que un cajón se asoma encima de la
        // partida, que es el fallo que D56 arregló.
        var stabilizer = new ResourceModeStabilizer(confirmations: 3);
        stabilizer.Stabilize(ResourceGovernorDecision.Normal);

        Assert.Equal(ResourceMode.Game, stabilizer.Stabilize(Game()).Mode);
        Assert.Equal(ResourceMode.Normal, stabilizer.Stabilize(ResourceGovernorDecision.Normal).Mode);
    }

    [Fact]
    public void TheFirstReadingIsAlwaysTaken()
    {
        var stabilizer = new ResourceModeStabilizer();

        Assert.Equal(ResourceMode.Busy, stabilizer.Stabilize(Busy()).Mode);
    }

    [Fact]
    public void WhileTheModeHolds_TheWordingStaysCurrent()
    {
        // Los porcentajes cambian aunque el modo no, y el texto es lo que se enseña.
        var stabilizer = new ResourceModeStabilizer(confirmations: 2);
        stabilizer.Stabilize(Busy());

        var refreshed = new ResourceGovernorDecision(
            ResourceMode.Busy,
            "El equipo está ocupado: GPU 91%.",
            AllowLocalCommands: true,
            AllowLocalAi: false,
            AllowRemoteAi: true,
            AllowVision: false,
            PauseWakeWord: false,
            SuppressTransientOverlays: false);

        Assert.Equal("El equipo está ocupado: GPU 91%.", stabilizer.Stabilize(refreshed).Reason);
    }
}
