using Nexo.Core.Commands.CommandCenter;

namespace Nexo.Core.Tests.Commands;

/// <summary>
/// Diseño D2 — registro, búsqueda y ejecución del Sakura Command Center. Todo el núcleo del
/// Command Center es lógica pura, así que se prueba sin levantar WPF.
/// </summary>
public sealed class CommandCenterTests
{
    private static SakuraCommandDescriptor Command(
        string id,
        string title,
        string description = "",
        SakuraCommandCategory category = SakuraCommandCategory.Navigation,
        IReadOnlyList<string>? keywords = null,
        Func<CancellationToken, Task<CommandExecutionResult>>? execute = null,
        Func<SakuraCommandAvailability>? availability = null) =>
        new(
            id,
            title,
            description,
            category,
            execute ?? (_ => Task.FromResult(CommandExecutionResult.Success())),
            keywords,
            availability: availability);

    // ---------- Registro ----------

    [Fact]
    public void Registry_KeepsRegistrationOrder()
    {
        var registry = new SakuraCommandRegistry();
        registry.Register(Command("a", "Ir a Inicio"));
        registry.Register(Command("b", "Ir a Sistema"));

        Assert.Equal(["a", "b"], registry.Commands.Select(command => command.Id));
        Assert.Equal(2, registry.Count);
    }

    [Fact]
    public void Registry_RejectsDuplicateIds()
    {
        var registry = new SakuraCommandRegistry();
        registry.Register(Command("go.home", "Ir a Inicio"));

        // Dos acciones distintas bajo el mismo identificador estable es un error de programación,
        // no algo que deba resolverse en silencio.
        var exception = Assert.Throws<InvalidOperationException>(
            () => registry.Register(Command("go.home", "Otra cosa")));
        Assert.Contains("go.home", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Registry_FindsById_IgnoringCaseAndSurroundingSpace()
    {
        var registry = new SakuraCommandRegistry();
        registry.Register(Command("go.system", "Ir a Sistema"));

        Assert.NotNull(registry.Find("GO.SYSTEM"));
        Assert.NotNull(registry.Find("  go.system  "));
        Assert.True(registry.Contains("go.system"));
        Assert.Null(registry.Find("no.existe"));
        Assert.Null(registry.Find(null));
    }

    [Fact]
    public void Descriptor_RequiresStableIdAndTitle()
    {
        Assert.Throws<ArgumentException>(() => Command(" ", "Título"));
        Assert.Throws<ArgumentException>(() => Command("id", " "));
        Assert.Throws<ArgumentNullException>(() => new SakuraCommandDescriptor(
            "id", "Título", "", SakuraCommandCategory.Shell, execute: null!));
    }

    // ---------- Normalización de la búsqueda ----------

    [Theory]
    [InlineData("Enfoque", "enfoque")]
    [InlineData("  ENFOQUE  ", "enfoque")]
    [InlineData("Personalización", "personalizacion")]
    [InlineData("Ir   a    Inicio", "ir a inicio")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void Normalize_IgnoresCaseAccentsAndExtraSpace(string? input, string expected) =>
        Assert.Equal(expected, CommandSearchEngine.Normalize(input));

    // ---------- Búsqueda ----------

    [Fact]
    public void Search_WithEmptyQuery_ReturnsEverythingInRegistrationOrder()
    {
        var commands = new[] { Command("a", "Ir a Inicio"), Command("b", "Ir a Audio") };

        var results = CommandSearchEngine.Search(commands, "   ");

        Assert.Equal(["a", "b"], results.Select(command => command.Id));
    }

    [Fact]
    public void Search_PrefersTitleOverKeywordsAndDescription()
    {
        var byDescription = Command("c", "Rutinas", description: "silenciar el audio del equipo");
        var byKeyword = Command("b", "Sistema", keywords: ["silenciar"]);
        var byTitle = Command("a", "Silenciar audio");

        var results = CommandSearchEngine.Search([byDescription, byKeyword, byTitle], "silenciar");

        Assert.Equal(["a", "b", "c"], results.Select(command => command.Id));
    }

    [Fact]
    public void Search_FindsByKeywordAlias()
    {
        var commands = new[] { Command("tasks", "Hoy", keywords: ["tareas", "pendientes"]) };

        Assert.Single(CommandSearchEngine.Search(commands, "pendientes"));
        Assert.Single(CommandSearchEngine.Search(commands, "TAREAS"));
    }

    [Fact]
    public void Search_IgnoresAccentsInQueryAndTitle()
    {
        var commands = new[] { Command("settings", "Personalización") };

        Assert.Single(CommandSearchEngine.Search(commands, "personalizacion"));
        Assert.Single(CommandSearchEngine.Search(commands, "PERSONALIZACIÓN"));
    }

    [Fact]
    public void Search_ExcludesCommandsThatDoNotMatch()
    {
        var commands = new[] { Command("a", "Ir a Inicio"), Command("b", "Ir a Audio") };

        Assert.Empty(CommandSearchEngine.Search(commands, "zzz"));
    }

    [Fact]
    public void Search_RanksExactTitleAboveStartsWith()
    {
        var startsWith = Command("b", "Audio del sistema");
        var exact = Command("a", "Audio");

        var results = CommandSearchEngine.Search([startsWith, exact], "audio");

        Assert.Equal("a", results[0].Id);
    }

    [Fact]
    public void Search_IsDeterministic_ForEquallyScoredCommands()
    {
        // Mismo título, misma puntuación: debe ganar siempre el orden de registro, nunca un
        // orden arbitrario que cambie entre ejecuciones.
        var commands = new[] { Command("first", "Enfoque"), Command("second", "Enfoque") };

        var a = CommandSearchEngine.Search(commands, "enfoque");
        var b = CommandSearchEngine.Search(commands, "enfoque");

        Assert.Equal(["first", "second"], a.Select(command => command.Id));
        Assert.Equal(a.Select(command => command.Id), b.Select(command => command.Id));
    }

    // ---------- Disponibilidad ----------

    [Fact]
    public void Availability_Unavailable_RequiresAReason() =>
        Assert.Throws<ArgumentException>(() => SakuraCommandAvailability.Unavailable("  "));

    [Fact]
    public void Availability_DefaultsToAvailable() =>
        Assert.True(Command("a", "Título").GetAvailability().IsAvailable);

    [Fact]
    public void Availability_IsEvaluatedOnEachQuery_NotCachedAtRegistration()
    {
        var enabled = false;
        var command = Command(
            "focus.end",
            "Finalizar enfoque",
            availability: () => enabled
                ? SakuraCommandAvailability.Available
                : SakuraCommandAvailability.Unavailable("No hay ninguna sesión de enfoque activa."));

        Assert.False(command.GetAvailability().IsAvailable);
        enabled = true;
        Assert.True(command.GetAvailability().IsAvailable);
    }

    [Fact]
    public void Availability_ThatThrows_DisablesTheCommandInsteadOfBreakingThePalette()
    {
        var command = Command(
            "audio.mute",
            "Silenciar audio",
            availability: () => throw new InvalidOperationException("mezclador caído"));

        var availability = command.GetAvailability();

        Assert.False(availability.IsAvailable);
        Assert.Contains("mezclador caído", availability.UnavailableReason!, StringComparison.Ordinal);
    }

    // ---------- Ejecución ----------

    [Fact]
    public async Task Execute_ReturnsSuccess_WhenCommandSucceeds()
    {
        var command = Command("a", "Título", execute: _ => Task.FromResult(CommandExecutionResult.Success("Hecho.")));

        var result = await command.ExecuteAsync();

        Assert.True(result.Succeeded);
        Assert.Equal("Hecho.", result.Message);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Execute_DoesNotRunUnavailableCommands_AndExplainsWhy()
    {
        var ran = false;
        var command = Command(
            "a",
            "Título",
            execute: _ =>
            {
                ran = true;
                return Task.FromResult(CommandExecutionResult.Success());
            },
            availability: () => SakuraCommandAvailability.Unavailable("El mezclador no está disponible."));

        var result = await command.ExecuteAsync();

        Assert.False(ran);
        Assert.False(result.Succeeded);
        Assert.Equal("El mezclador no está disponible.", result.Message);
    }

    [Fact]
    public async Task Execute_TranslatesExceptionToFailure_PreservingTheOriginalForLogging()
    {
        var thrown = new InvalidOperationException("el servicio falló");
        var command = Command("audio.mute", "Silenciar audio", execute: _ => throw thrown);

        // Una excepción dentro de un comando no debe propagarse a la UI ni cerrar Sakura, pero
        // tampoco debe perderse: el stack trace original se conserva para el registro.
        var result = await command.ExecuteAsync();

        Assert.False(result.Succeeded);
        Assert.Same(thrown, result.Error);
        Assert.Contains("Silenciar audio", result.Message!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Execute_ReportsCancellationAsFailure_NotAsSuccess()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var command = Command(
            "slow",
            "Acción larga",
            execute: token => Task.FromException<CommandExecutionResult>(new OperationCanceledException(token)));

        var result = await command.ExecuteAsync(cancellation.Token);

        Assert.False(result.Succeeded);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Execute_NeverReportsSuccessWhenTheCommandReturnsFailure()
    {
        var command = Command(
            "a",
            "Título",
            execute: _ => Task.FromResult(CommandExecutionResult.Failure("No había nada que hacer.")));

        var result = await command.ExecuteAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("No había nada que hacer.", result.Message);
    }

    [Fact]
    public void ExecutionResult_FailureRequiresAMessage()
    {
        Assert.Throws<ArgumentException>(() => CommandExecutionResult.Failure(" "));
        Assert.Throws<ArgumentNullException>(
            () => CommandExecutionResult.FromException("mensaje", null!));
    }
}
