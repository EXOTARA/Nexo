using Nexo.Core.Memory;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D10 (Fase 6) — la memoria ya se llena desde la conversación. Lo que se prueba aquí es
/// sobre todo lo que el detector NO propone: inferir recuerdos de más sería la vigilancia
/// silenciosa que el roadmap prohíbe para esta fase.
/// </summary>
public sealed class MemoryCandidateTests
{
    // ---------- Órdenes explícitas ----------

    [Theory]
    [InlineData("recuerda que mi editor es Visual Studio")]
    [InlineData("Recuerda que mi editor es Visual Studio.")]
    [InlineData("acuérdate de que mi editor es Visual Studio")]
    [InlineData("quiero que recuerdes que mi editor es Visual Studio")]
    [InlineData("ten en cuenta que mi editor es Visual Studio")]
    public void ExplicitRequests_AreDetected_AndKeepOnlyTheFact(string prompt)
    {
        var candidate = MemoryCandidateDetector.Detect(prompt);

        Assert.NotNull(candidate);
        Assert.Equal(MemoryCandidateSource.Explicito, candidate!.Source);
        Assert.Equal("mi editor es Visual Studio", candidate.Text);
    }

    [Fact]
    public void AnExplicitRequestAboutAPreference_IsFiledAsAPreference()
    {
        // La categoría la decide el contenido, no el disparador.
        var candidate = MemoryCandidateDetector.Detect("recuerda que prefiero respuestas cortas");

        Assert.NotNull(candidate);
        Assert.Equal(MemoryCategory.Preferencias, candidate!.Category);
        Assert.Equal("prefiero respuestas cortas", candidate.Text);
    }

    [Fact]
    public void AnExplicitRequestThatIsNotAPreference_IsFiledAsConversation()
    {
        var candidate = MemoryCandidateDetector.Detect("recuerda que el proyecto se llama Sakura");

        Assert.NotNull(candidate);
        Assert.Equal(MemoryCategory.Conversacion, candidate!.Category);
    }

    // ---------- Preferencias dichas de paso ----------

    [Theory]
    [InlineData("prefiero respuestas cortas")]
    [InlineData("no me gusta que me hables de usted")]
    [InlineData("me gusta más el modo oscuro")]
    public void PreferencesStatedInPassing_AreOnlyObserved_NeverExplicit(string prompt)
    {
        var candidate = MemoryCandidateDetector.Detect(prompt);

        Assert.NotNull(candidate);
        Assert.Equal(MemoryCandidateSource.Observado, candidate!.Source);
        Assert.Equal(MemoryCategory.Preferencias, candidate.Category);
    }

    // ---------- Lo que NO se detecta ----------

    [Theory]
    [InlineData("recuérdame comprar pan")]
    [InlineData("recuerdame comprar pan")]
    [InlineData("apunta comprar pan")]
    public void ReminderPhrases_AreLeftToTheTaskParser(string prompt)
    {
        // "recuérdame" y "apunta" ya crean tareas. Robárselas convertiría un recordatorio en un
        // recuerdo, que es justo lo que la persona NO pidió.
        Assert.Null(MemoryCandidateDetector.Detect(prompt));
    }

    [Theory]
    [InlineData("¿prefiero café o té?")]
    [InlineData("prefiero café o té?")]
    public void Questions_DeclareNothing_AndAreNotRemembered(string prompt) =>
        Assert.Null(MemoryCandidateDetector.Detect(prompt));

    [Fact]
    public void APreferenceMentionedMidSentence_IsNotADeclaration()
    {
        // El patrón está anclado al principio a propósito: aquí no se declara ninguna preferencia.
        Assert.Null(MemoryCandidateDetector.Detect("no sé si prefiero el modo oscuro"));
    }

    [Fact]
    public void HabitsAreNeverInferredFromASingleSentence()
    {
        // Un hábito es conducta observada en el tiempo. Sacarlo de una frase sería inventarlo.
        var candidates = new[]
        {
            "recuerda que siempre trabajo de noche",
            "prefiero trabajar de noche",
            "suelo trabajar de noche"
        }.Select(MemoryCandidateDetector.Detect);

        Assert.DoesNotContain(candidates, candidate => candidate?.Category == MemoryCategory.Habitos);
    }

    [Fact]
    public void AVeryLongStatement_IsAParagraph_NotAMemory()
    {
        var longFact = new string('a', 250);

        Assert.Null(MemoryCandidateDetector.Detect($"recuerda que {longFact}"));
    }

    [Fact]
    public void AnEmptyOrTrivialFact_IsNotRemembered()
    {
        Assert.Null(MemoryCandidateDetector.Detect("recuerda que "));
        Assert.Null(MemoryCandidateDetector.Detect("recuerda que sí"));
        Assert.Null(MemoryCandidateDetector.Detect(null));
    }
}
