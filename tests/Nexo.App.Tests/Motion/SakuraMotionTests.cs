using System.Windows;
using System.Windows.Media.Animation;
using Nexo.App.Motion;
using Xunit;

namespace Nexo.App.Tests.Motion;

/// <summary>
/// Regresión del defecto que dejó la aplicación entera invisible.
///
/// En WPF, <see cref="Timeline.BeginTime"/> con valor <c>null</c> no significa "empieza ya": significa
/// que la línea de tiempo **no se reproduce nunca**. El ayudante de movimiento recibe el retraso como
/// <c>TimeSpan?</c> —vacío cuando no hay escalonado— y lo pasaba tal cual, así que toda animación sin
/// escalonado quedaba desactivada en silencio: el shell se mostraba con opacidad 0, los módulos salían
/// en blanco y las secciones de Ajustes no abrían.
///
/// No falló ninguna prueba y tampoco lo vi al probarlo en pantalla, porque el equipo tenía las
/// animaciones de Windows desactivadas y todo iba por la ruta sin animación. Solo apareció cuando
/// alguien las activó. De ahí que esto se compruebe sobre el objeto de animación y no sobre la
/// pantalla: es lo único que se puede afirmar sin depender de un ajuste del sistema.
/// </summary>
public sealed class SakuraMotionTests
{
    private static readonly IEasingFunction Easing = new CubicBezierEase();
    private static readonly Duration OneSecond = new(TimeSpan.FromSeconds(1));

    [Fact]
    public void AnAnimationWithoutAStaggerStillPlays()
    {
        var animation = SakuraMotion.CreateAnimation(1, OneSecond, Easing);

        Assert.NotNull(animation.BeginTime);
        Assert.Equal(TimeSpan.Zero, animation.BeginTime);
    }

    [Fact]
    public void AnExplicitlyEmptyStaggerStillPlays()
    {
        // El caso exacto que se rompía: el retraso llega como TimeSpan? vacío desde la ruta normal.
        var animation = SakuraMotion.CreateAnimation(1, OneSecond, Easing, beginTime: null);

        Assert.Equal(TimeSpan.Zero, animation.BeginTime);
    }

    [Fact]
    public void AStaggerIsRespectedWhenThereIsOne()
    {
        var delay = TimeSpan.FromMilliseconds(90);

        var animation = SakuraMotion.CreateAnimation(1, OneSecond, Easing, delay);

        Assert.Equal(delay, animation.BeginTime);
    }

    [Fact]
    public void TheAnimationCarriesItsTargetDurationAndCurve()
    {
        var animation = SakuraMotion.CreateAnimation(0.75, OneSecond, Easing);

        Assert.Equal(0.75, animation.To);
        Assert.Equal(OneSecond, animation.Duration);
        Assert.Same(Easing, animation.EasingFunction);
    }

    [Fact]
    public void EveryStaggerStepProducesAPlayableDelay()
    {
        // StaggerAt alimenta BeginTime en las entradas escalonadas. Un valor negativo o nulo en
        // cualquier posición reintroduciría el mismo defecto para ese elemento en concreto.
        for (var index = 0; index < 40; index++)
        {
            var delay = SakuraMotion.StaggerAt(index);

            Assert.True(
                delay >= TimeSpan.Zero,
                $"El retraso de la posición {index} era negativo: {delay}.");
            Assert.True(
                delay <= SakuraMotion.MaximumStagger,
                $"El retraso de la posición {index} superó el tope: {delay}.");

            var animation = SakuraMotion.CreateAnimation(1, OneSecond, Easing, delay);
            Assert.NotNull(animation.BeginTime);
        }
    }

    [Fact]
    public void TheFirstItemOfAStaggeredListStartsImmediately()
    {
        Assert.Equal(TimeSpan.Zero, SakuraMotion.StaggerAt(0));
    }

    [Fact]
    public void TheStaggerStopsGrowingOnceItHitsTheCap()
    {
        // Sin tope, una lista larga haría esperar al último elemento más de lo que dura la propia
        // animación, y entonces deja de leerse como coreografía y pasa a leerse como lentitud.
        Assert.Equal(SakuraMotion.MaximumStagger, SakuraMotion.StaggerAt(500));
        Assert.Equal(SakuraMotion.StaggerAt(100), SakuraMotion.StaggerAt(500));
    }
}
