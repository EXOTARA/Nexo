using Nexo.Core.Updates;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D62 — cuál de dos versiones es más nueva.
///
/// Estas pruebas existen antes que el actualizador a propósito. Un actualizador que se equivoca
/// aquí no da ninguna señal de que algo va mal: simplemente deja de ofrecer actualizaciones, o
/// peor, ofrece una vieja. Es el tipo de fallo que se descubre meses después y sin pista.
/// </summary>
public sealed class KohanaVersionTests
{
    private static KohanaVersion Parse(string text)
    {
        Assert.True(KohanaVersion.TryParse(text, out var version), $"No se pudo leer «{text}».");
        return version;
    }

    [Fact]
    public void TenIsNewerThanFive_NotOlder()
    {
        // El clásico: comparado como texto, «0.9.10» va antes que «0.9.5» porque el 1 precede al 5.
        // Un actualizador que se cree eso deja de funcionar justo cuando las versiones empiezan a
        // acumularse.
        Assert.True(Parse("0.9.10") > Parse("0.9.5"));
    }

    [Fact]
    public void AFinalRelease_BeatsItsOwnPreRelease()
    {
        // Es al revés de lo que sugiere la longitud del texto, y es la regla que hace que salir de
        // beta cuente como actualización.
        Assert.True(Parse("0.9.5") > Parse("0.9.5-beta"));
    }

    [Fact]
    public void BuildMetadata_DoesNotCount()
    {
        // La versión informativa de la compilación lleva el commit pegado. Si contara, cada
        // compilación parecería una versión nueva y Kohana se ofrecería actualizarse a sí misma.
        Assert.Equal(
            0,
            Parse("0.9.5-beta+563688b44104984ff2f1e422848a21e905b2b9a0")
                .CompareTo(Parse("0.9.5-beta")));
    }

    [Fact]
    public void AFourthNumber_IsIgnored()
    {
        // Windows escribe cuatro números y el proyecto publica tres. Tratar el cuarto como
        // significativo haría que «0.9.5.0» y «0.9.5» parecieran distintas.
        Assert.Equal(0, Parse("0.9.5.0").CompareTo(Parse("0.9.5")));
    }

    [Theory]
    [InlineData("1.0.0", "0.9.9")]
    [InlineData("0.10.0", "0.9.9")]
    [InlineData("0.9.6", "0.9.5")]
    [InlineData("0.9.5-beta.10", "0.9.5-beta.9")]
    [InlineData("0.9.5-beta.1", "0.9.5-beta")]
    [InlineData("0.9.5-beta", "0.9.5-alpha")]
    public void TheFirstIsNewerThanTheSecond(string newer, string older) =>
        Assert.True(Parse(newer) > Parse(older));

    [Fact]
    public void ANumericPreReleasePart_SortsBeforeAWordyOne() =>
        Assert.True(Parse("0.9.5-1") < Parse("0.9.5-beta"));

    [Fact]
    public void TheSameVersionIsNotNewerThanItself()
    {
        // Lo que evita que Kohana se ofrezca actualizarse a lo que ya tiene puesto.
        Assert.Equal(0, Parse("0.9.5-beta").CompareTo(Parse("0.9.5-beta")));
        Assert.False(Parse("0.9.5-beta") > Parse("0.9.5-beta"));
    }

    [Fact]
    public void ALeadingV_IsAccepted()
    {
        // Las etiquetas de publicación suelen llegar como «v1.2.3».
        Assert.Equal(0, Parse("v0.9.5").CompareTo(Parse("0.9.5")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no soy una versión")]
    [InlineData("0.9.beta")]
    [InlineData("-1.0.0")]
    [InlineData("1.2.3.4.5")]
    public void RubbishIsRefused_NotGuessed(string? text)
    {
        // Esto se lee de algo que llega de la red. Un servidor que responde cualquier cosa —una
        // página de error, HTML, una redirección— es un caso normal, y adivinar una versión a
        // partir de ella es cómo se instala algo que nadie publicó.
        Assert.False(KohanaVersion.TryParse(text, out _));
    }

    [Fact]
    public void ItReadsBackAsItWasWritten()
    {
        Assert.Equal("0.9.5-beta", Parse("0.9.5-beta").ToString());
        Assert.Equal("1.0.0", Parse("1.0.0").ToString());
    }
}
