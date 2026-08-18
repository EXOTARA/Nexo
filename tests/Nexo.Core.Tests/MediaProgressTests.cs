using Nexo.Core.Media;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D43 — el tiempo de la pista. Cada caso de aquí es una forma real en la que Windows
/// entrega una posición que no cuadra, y cada una rompe la barra de progreso de otra manera.
/// </summary>
public sealed class MediaProgressTests
{
    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(15, "0:15")]
    [InlineData(65, "1:05")]
    [InlineData(245, "4:05")]
    public void Format_UnderAnHour_UsesMinutesAndSeconds(int seconds, string expected) =>
        Assert.Equal(expected, MediaProgress.Format(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void Format_OverAnHour_AddsTheHour()
    {
        // Un pódcast de hora y pico enseñaría «2:03» sin las horas: dos minutos y tres segundos,
        // que es lo contrario de lo que dura.
        Assert.Equal("1:02:03", MediaProgress.Format(new TimeSpan(1, 2, 3)));
    }

    [Fact]
    public void Format_WithoutTime_ReadsZero() =>
        Assert.Equal("0:00", MediaProgress.Format(null));

    [Fact]
    public void Format_NegativeTime_ReadsZero()
    {
        // Un reproductor recién abierto llega a informar posiciones negativas. «-1:-30» no es un
        // tiempo.
        Assert.Equal("0:00", MediaProgress.Format(TimeSpan.FromSeconds(-90)));
    }

    [Fact]
    public void Fraction_Halfway_IsAHalf() =>
        Assert.Equal(0.5, MediaProgress.Fraction(TimeSpan.FromSeconds(50), TimeSpan.FromSeconds(100)));

    [Fact]
    public void Fraction_WithoutDuration_IsUnknown()
    {
        // Una emisión en directo informa duración cero. Cero y «no se sabe» no son lo mismo: la
        // barra no puede pintarse al principio de algo que no tiene final.
        Assert.Null(MediaProgress.Fraction(TimeSpan.FromSeconds(30), TimeSpan.Zero));
        Assert.Null(MediaProgress.Fraction(TimeSpan.FromSeconds(30), null));
    }

    [Fact]
    public void Fraction_PositionBeyondDuration_ClampsToTheEnd()
    {
        // Al cambiar de pista, la posición de la anterior llega junto a la duración de la nueva. Sin
        // tope, el relleno se dibuja más ancho que su carril y se sale de la tarjeta.
        Assert.Equal(1, MediaProgress.Fraction(TimeSpan.FromSeconds(400), TimeSpan.FromSeconds(100)));
    }

    [Fact]
    public void Fraction_NegativePosition_ClampsToTheStart() =>
        Assert.Equal(0, MediaProgress.Fraction(TimeSpan.FromSeconds(-5), TimeSpan.FromSeconds(100)));
}
