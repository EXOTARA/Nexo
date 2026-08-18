using Nexo.Core.Shell;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D43 — qué pestaña del panel superior se enseña al abrir Sistema.
/// </summary>
public sealed class DashboardTabPolicyTests
{
    [Fact]
    public void Resolve_KeepsTheLastTabUsed() =>
        Assert.Equal(
            DashboardTab.Performance,
            DashboardTabPolicy.Resolve(DashboardTab.Performance, somethingIsPlaying: false));

    [Fact]
    public void Resolve_WithMusicPlaying_KeepsMedia() =>
        Assert.Equal(
            DashboardTab.Media,
            DashboardTabPolicy.Resolve(DashboardTab.Media, somethingIsPlaying: true));

    [Fact]
    public void Resolve_WithoutMusic_DoesNotOpenAnEmptyPlayer()
    {
        // Volver a Media cuando no suena nada enseña el estado vacío nada más entrar, y obliga a un
        // clic para llegar a algo que sí tenga contenido.
        Assert.Equal(
            DashboardTab.Panel,
            DashboardTabPolicy.Resolve(DashboardTab.Media, somethingIsPlaying: false));
    }

    [Fact]
    public void Resolve_MusicPlaying_DoesNotHijackAnotherTab()
    {
        // Que suene música no es motivo para sacar a nadie de Rendimiento: la regla protege de una
        // pestaña vacía, no decide por el usuario.
        Assert.Equal(
            DashboardTab.Performance,
            DashboardTabPolicy.Resolve(DashboardTab.Performance, somethingIsPlaying: true));
    }
}
