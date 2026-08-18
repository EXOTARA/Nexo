using Nexo.Core.Shell;
using Xunit;

namespace Nexo.Core.Tests;

public sealed class AnswerPillPolicyTests
{
    [Fact]
    public void AShortAnswerStillStaysLongEnoughToNotice()
    {
        // «Sí, el 15 de septiembre» se lee en un segundo, pero la píldora aparece mientras la
        // persona está en otra cosa: hace falta el tiempo de darse cuenta, mirar y volver.
        var visible = AnswerPillPolicy.ReadingTimeFor("Sí, el 15 de septiembre.");

        Assert.Equal(AnswerPillPolicy.MinimumVisible, visible);
    }

    [Fact]
    public void ALongerAnswerGetsProportionallyMoreTime()
    {
        var shortAnswer = string.Join(' ', Enumerable.Repeat("palabra", 30));
        var longAnswer = string.Join(' ', Enumerable.Repeat("palabra", 120));

        Assert.True(
            AnswerPillPolicy.ReadingTimeFor(longAnswer) > AnswerPillPolicy.ReadingTimeFor(shortAnswer),
            "Una respuesta más larga debería quedarse más tiempo.");
    }

    [Fact]
    public void AVeryLongAnswerIsCappedInsteadOfHoveringForever()
    {
        // Sin tope, una respuesta de mil palabras dejaría una ventana flotando cinco minutos encima
        // de lo que la persona esté haciendo. Pasado el tope lo razonable es abrirla en Kohana.
        var enormous = string.Join(' ', Enumerable.Repeat("palabra", 5000));

        Assert.Equal(AnswerPillPolicy.MaximumVisible, AnswerPillPolicy.ReadingTimeFor(enormous));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    public void NoAnswerFallsBackToTheMinimumInsteadOfZero(string? text)
    {
        // Un cero aquí haría desaparecer la píldora en el mismo fotograma en que aparece.
        Assert.Equal(AnswerPillPolicy.MinimumVisible, AnswerPillPolicy.ReadingTimeFor(text));
    }

    [Fact]
    public void TheReadingTimeMatchesTheDeclaredReadingSpeed()
    {
        // 540 palabras a 180 por minuto son tres minutos, que se recorta al tope. Se comprueba con
        // una cifra que caiga dentro del rango para que la fórmula quede atada, no solo los bordes.
        var ninetyWords = string.Join(' ', Enumerable.Repeat("palabra", 90));

        var visible = AnswerPillPolicy.ReadingTimeFor(ninetyWords);

        Assert.Equal(30, visible.TotalSeconds, precision: 1);
    }

    [Fact]
    public void WordsAreCountedByRunsOfNonSpaceNotBySplittingOnSingleSpaces()
    {
        // Saltos de línea, tabuladores y espacios dobles son lo normal en una respuesta de IA. Un
        // recuento ingenuo los contaría como palabras vacías e inflaría el tiempo en pantalla.
        var messy = "  hola\n\nmundo\t\tde   nuevo  ";

        Assert.Equal(
            AnswerPillPolicy.ReadingTimeFor("hola mundo de nuevo"),
            AnswerPillPolicy.ReadingTimeFor(messy));
    }

    [Fact]
    public void AShortAnswerDoesNotOfferOpeningInKohana()
    {
        Assert.False(AnswerPillPolicy.DeservesOpeningInKohana("Listo, ya está."));
    }

    [Fact]
    public void AnAnswerThatDoesNotFitOffersOpeningInKohana()
    {
        var essay = new string('a', AnswerPillPolicy.LongAnswerCharacters + 1);

        Assert.True(AnswerPillPolicy.DeservesOpeningInKohana(essay));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NoAnswerNeverOffersOpeningInKohana(string? text)
    {
        Assert.False(AnswerPillPolicy.DeservesOpeningInKohana(text));
    }
}
