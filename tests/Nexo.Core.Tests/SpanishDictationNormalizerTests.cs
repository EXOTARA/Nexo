using Nexo.Core.Flow;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D6.1 (Fase 3 — Sakura Flow) — el normalizador de DICTADO, que no debe confundirse con
/// <c>SpanishVoiceTranscriptNormalizer</c> (el de comandos, deliberadamente destructivo). Estas
/// pruebas fijan sobre todo lo que NO debe hacer: no borrar palabras reales que suenan a muletilla,
/// no pegar guiones que la persona dictó bien, no capitalizar un correo.
/// </summary>
public sealed class SpanishDictationNormalizerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_WithNothingToSay_ReturnsEmpty(string? transcript)
    {
        Assert.Equal(string.Empty, SpanishDictationNormalizer.Normalize(transcript));
    }

    // ---------- Puntuación hablada ----------

    [Fact]
    public void Normalize_SpokenPeriod_BecomesPunctuationAttachedToTheWord()
    {
        Assert.Equal("Hola.", SpanishDictationNormalizer.Normalize("hola punto"));
    }

    [Fact]
    public void Normalize_SpokenComma_AttachesLeftAndKeepsOneSpaceRight()
    {
        Assert.Equal("Hola, ¿cómo estás?",
            SpanishDictationNormalizer.Normalize("hola coma abre interrogacion cómo estás cierra interrogacion"));
    }

    [Fact]
    public void Normalize_LongerPhrasesWinOverShorterOnes()
    {
        // "punto y aparte" no debe resolverse como "punto" + "y aparte".
        var result = SpanishDictationNormalizer.Normalize("primera idea punto y aparte segunda idea");

        Assert.Equal("Primera idea\n\nSegunda idea", result);
    }

    [Fact]
    public void Normalize_PuntoYComa_IsNotConfusedWithPuntoThenComa()
    {
        Assert.Equal("Uno; dos", SpanishDictationNormalizer.Normalize("uno punto y coma dos"));
    }

    [Fact]
    public void Normalize_AcceptsAccentedAndUnaccentedSpellings()
    {
        // Whisper puede escribir la orden con o sin tilde según el audio.
        Assert.Equal("Uno\nDos", SpanishDictationNormalizer.Normalize("uno nueva línea dos"));
        Assert.Equal("Uno\nDos", SpanishDictationNormalizer.Normalize("uno nueva linea dos"));
    }

    [Fact]
    public void Normalize_CapitalizesEachSentence()
    {
        Assert.Equal("Uno. Dos. Tres.",
            SpanishDictationNormalizer.Normalize("uno punto dos punto tres punto"));
    }

    // ---------- Muletillas ----------

    [Fact]
    public void Normalize_RemovesMeaninglessHesitationSounds()
    {
        Assert.Equal("Quiero un café",
            SpanishDictationNormalizer.Normalize("eh quiero un mmm café"));
    }

    [Theory]
    [InlineData("este archivo es importante", "Este archivo es importante")]
    [InlineData("bueno para ti", "Bueno para ti")]
    [InlineData("o sea que no viene", "O sea que no viene")]
    [InlineData("pues no sé", "Pues no sé")]
    public void Normalize_NeverRemovesWordsThatAreAlsoRealSpanish(string spoken, string expected)
    {
        // Aunque se usen como muletillas, borrarlas destruiría frases legítimas. Es una decisión
        // explícita del diseño, no un descuido de la lista.
        Assert.Equal(expected, SpanishDictationNormalizer.Normalize(spoken));
    }

    [Fact]
    public void Normalize_WithFillerRemovalDisabled_KeepsThemAll()
    {
        var options = FlowDictationOptions.Default with { RemoveFillers = false };

        Assert.Equal("Eh quiero un café",
            SpanishDictationNormalizer.Normalize("eh quiero un café", options));
    }

    // ---------- Modo Código ----------

    [Fact]
    public void Codigo_DoesNotCapitalize()
    {
        var options = FlowDictationOptions.Default with { Mode = FlowMode.Codigo };

        Assert.Equal("var total = cero", SpanishDictationNormalizer.Normalize("var total igual cero", options));
    }

    [Fact]
    public void Codigo_UnderstandsProgrammingSymbols()
    {
        var options = FlowDictationOptions.Default with { Mode = FlowMode.Codigo };

        Assert.Equal("if abre parentesis",
            SpanishDictationNormalizer.Normalize("if abre parentesis", options).Replace("(", "abre parentesis"));
        Assert.Equal("{", SpanishDictationNormalizer.Normalize("abre llave", options));
        Assert.Equal("}", SpanishDictationNormalizer.Normalize("cierra llave", options));
        Assert.Equal("=>", SpanishDictationNormalizer.Normalize("flecha", options));
    }

    [Fact]
    public void Texto_LeavesProgrammingWordsAlone()
    {
        // "llave" en prosa es una llave de puerta, no un carácter.
        Assert.Equal("Perdí la llave", SpanishDictationNormalizer.Normalize("perdí la llave"));
        Assert.Equal("Me da igual", SpanishDictationNormalizer.Normalize("me da igual"));
    }

    [Fact]
    public void GuionBajo_AttachesOnBothSidesForIdentifiers()
    {
        var options = FlowDictationOptions.Default with { Mode = FlowMode.Codigo };

        Assert.Equal("total_general", SpanishDictationNormalizer.Normalize("total guion bajo general", options));
    }

    [Fact]
    public void Normalize_DoesNotGlueDashesThatWhisperWroteItself()
    {
        // Un guion con espacios que vino de Whisper es un inciso legítimo: no debe pegarse.
        Assert.Equal("Esto - aquello", SpanishDictationNormalizer.Normalize("esto - aquello"));
    }

    // ---------- Correo ----------

    [Fact]
    public void Correo_BuildsAnAddressWithoutSpacesAndWithoutCapitalizingIt()
    {
        var options = FlowDictationOptions.Default with { Mode = FlowMode.Correo };

        Assert.Equal("adler@gmail.com",
            SpanishDictationNormalizer.Normalize("adler arroba gmail punto com", options));
    }

    [Fact]
    public void Correo_KeepsASentencePeriodAfterAnAddressInsteadOfSwallowingTheNextWord()
    {
        var options = FlowDictationOptions.Default with { Mode = FlowMode.Correo };

        // "luego" no es un dominio de primer nivel: el punto es fin de oración, no parte del correo.
        Assert.Equal("Escríbeme a adler@ejemplo.com. Luego te llamo.",
            SpanishDictationNormalizer.Normalize(
                "escríbeme a adler@ejemplo.com punto luego te llamo punto", options));
    }

    [Fact]
    public void Correo_RebuildsMultiLevelDomains()
    {
        var options = FlowDictationOptions.Default with { Mode = FlowMode.Correo };

        Assert.Equal("adler@ejemplo.com.mx",
            SpanishDictationNormalizer.Normalize("adler arroba ejemplo punto com punto mx", options));
    }

    [Fact]
    public void Correo_StillCapitalizesOrdinaryProse()
    {
        var options = FlowDictationOptions.Default with { Mode = FlowMode.Correo };

        Assert.Equal("Buenos días, te escribo por el reporte.",
            SpanishDictationNormalizer.Normalize("buenos días coma te escribo por el reporte punto", options));
    }

    // ---------- Diccionario y atajos ----------

    [Fact]
    public void Dictionary_FixesMisheardProperNouns()
    {
        var options = FlowDictationOptions.Default with
        {
            Dictionary = [new FlowDictionaryEntry("cojana", "Sakura")]
        };

        Assert.Equal("Abre Sakura ahora",
            SpanishDictationNormalizer.Normalize("abre cojana ahora", options));
    }

    [Fact]
    public void Dictionary_OnlyMatchesWholeWords()
    {
        var options = FlowDictationOptions.Default with
        {
            Dictionary = [new FlowDictionaryEntry("ola", "hola")]
        };

        // No debe tocar "ola" dentro de "olas".
        Assert.Equal("Las olas", SpanishDictationNormalizer.Normalize("las olas", options));
    }

    [Fact]
    public void Snippets_ExpandVerbatim()
    {
        var options = FlowDictationOptions.Default with
        {
            Snippets = [new FlowSnippet("mi correo", "adler@ejemplo.com")]
        };

        Assert.Equal("Escríbeme a adler@ejemplo.com",
            SpanishDictationNormalizer.Normalize("escríbeme a mi correo", options));
    }

    [Fact]
    public void Snippets_ExpansionIsNotReinterpretedAsSpokenPunctuation()
    {
        // La expansión contiene la palabra "punto": debe quedar tal cual, no volverse ".".
        var options = FlowDictationOptions.Default with
        {
            Snippets = [new FlowSnippet("mi lema", "punto y aparte siempre")]
        };

        Assert.Equal("Escribe punto y aparte siempre",
            SpanishDictationNormalizer.Normalize("escribe mi lema", options));
    }

    [Fact]
    public void DictionaryAndSnippets_IgnoreBlankEntries()
    {
        var options = FlowDictationOptions.Default with
        {
            Dictionary = [new FlowDictionaryEntry("   ", "x")],
            Snippets = [new FlowSnippet("", "y")]
        };

        Assert.Equal("Hola", SpanishDictationNormalizer.Normalize("hola", options));
    }

    // ---------- Espaciado ----------

    [Fact]
    public void Normalize_CollapsesRepeatedWhitespaceFromTheTranscript()
    {
        Assert.Equal("Hola mundo", SpanishDictationNormalizer.Normalize("  hola    mundo  "));
    }

    [Fact]
    public void Normalize_LeavesNoStrayWhitespaceAroundLineBreaks()
    {
        Assert.Equal("Uno\nDos", SpanishDictationNormalizer.Normalize("uno nueva linea dos"));
    }
}
