using Nexo.Core.Shell;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D58 — hacia dónde entra la pestaña nueva del cajón.
///
/// Las tres pestañas entraban igual, de abajo arriba, así que el movimiento decía «ha cambiado
/// algo» pero no «te has movido a la derecha». Con la dirección, la tira de arriba y el contenido
/// cuentan lo mismo, como al pasar la hoja de un libro.
/// </summary>
public sealed class DashboardTabDirectionTests
{
    [Fact]
    public void GoingForward_ArrivesFromTheRight() =>
        Assert.Equal(1, DashboardTabPolicy.EnterDirection(DashboardTab.Panel, DashboardTab.Media));

    [Fact]
    public void GoingBack_ArrivesFromTheLeft() =>
        Assert.Equal(-1, DashboardTabPolicy.EnterDirection(DashboardTab.Performance, DashboardTab.Media));

    [Fact]
    public void SkippingATab_KeepsTheDirection_NotTheDistance()
    {
        // De Panel a Rendimiento se salta Media, pero se sigue yendo hacia la derecha: la entrada
        // dice la dirección, no cuánto se ha saltado.
        Assert.Equal(1, DashboardTabPolicy.EnterDirection(DashboardTab.Panel, DashboardTab.Performance));
        Assert.Equal(-1, DashboardTabPolicy.EnterDirection(DashboardTab.Performance, DashboardTab.Panel));
    }

    [Fact]
    public void StayingPut_HasNoDirection() =>
        Assert.Equal(0, DashboardTabPolicy.EnterDirection(DashboardTab.Media, DashboardTab.Media));
}
