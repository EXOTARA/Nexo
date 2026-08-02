using Nexo.Core.Workspace;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D12 (Fase 5) — el modelo de confianza es explícito: "ninguna capacidad nueva puede empezar
/// en el nivel 6: cada una debe demostrarse en los niveles 1–3 antes de solicitar el salto a
/// ejecución". Esto lo comprueba.
/// </summary>
public sealed class WorkspaceAutonomyTests
{
    [Theory]
    [InlineData(WorkspaceAutonomyLevel.Ver)]
    [InlineData(WorkspaceAutonomyLevel.Guiar)]
    [InlineData(WorkspaceAutonomyLevel.Proponer)]
    public void LevelsOneToThree_AreAvailable(WorkspaceAutonomyLevel level) =>
        Assert.True(WorkspaceAutonomyPolicy.IsAvailable(level));

    /// <summary>
    /// Diseño D14 — el nivel 4 se abrió porque aparecieron las dos cosas que lo bloqueaban (el
    /// checkpoint por archivo y el Audit Log), no porque pasara un sprint.
    /// </summary>
    [Fact]
    public void LevelFour_IsAvailableSinceCheckpointsAndTheAuditLogExist()
    {
        Assert.True(WorkspaceAutonomyPolicy.IsAvailable(WorkspaceAutonomyLevel.EjecutarUnPaso));
        Assert.True(WorkspaceAutonomyPolicy.CanWrite(WorkspaceAutonomyLevel.EjecutarUnPaso));
    }

    [Theory]
    [InlineData(WorkspaceAutonomyLevel.Ver)]
    [InlineData(WorkspaceAutonomyLevel.Guiar)]
    [InlineData(WorkspaceAutonomyLevel.Proponer)]
    public void LevelsOneToThree_CanStillNotWrite(WorkspaceAutonomyLevel level) =>
        Assert.False(WorkspaceAutonomyPolicy.CanWrite(level));

    [Theory]
    [InlineData(WorkspaceAutonomyLevel.ColaborarConConfirmaciones)]
    [InlineData(WorkspaceAutonomyLevel.AutomatizarSecuencia)]
    public void ChainedLevels_AreNotAvailableYet_AndSayWhy(WorkspaceAutonomyLevel level)
    {
        // Encadenar pasos y automatizar una secuencia son problemas distintos de "hacer un cambio
        // confirmado", y ninguno se ha demostrado todavía.
        Assert.False(WorkspaceAutonomyPolicy.IsAvailable(level));
        Assert.False(WorkspaceAutonomyPolicy.CanWrite(level));
        Assert.Contains("encadenar", WorkspaceAutonomyPolicy.ExplainUnavailable(level));
    }

    [Fact]
    public void TheDefaultLevel_DoesNotRiseWithD14()
    {
        // Que escribir sea posible no significa que deba estar encendido: subir a nivel 4 es una
        // decisión de la persona, y una actualización no la toma por ella.
        Assert.Equal(WorkspaceAutonomyLevel.Guiar, WorkspaceAutonomyPolicy.Default);
        Assert.False(WorkspaceAutonomyPolicy.CanWrite(new WorkspaceSettings().AutonomyLevel));
    }

    [Fact]
    public void AnUnknownLevel_FailsClosed()
    {
        // Añadir valores al enum no debe escalar la autonomía en silencio.
        Assert.False(WorkspaceAutonomyPolicy.IsAvailable((WorkspaceAutonomyLevel)99));
    }

    // ---------- Ajustes ----------

    [Fact]
    public void ByDefault_NoFolderIsAuthorized()
    {
        var settings = new WorkspaceSettings();

        Assert.False(settings.HasAuthorizedFolder);
        Assert.Null(settings.AuthorizedAt);
    }

    [Fact]
    public void ASettingsFileCannotGrantALevelThePolicyDoesNotOffer()
    {
        // La autonomía no se hereda de un archivo escrito a mano, se concede.
        var settings = new WorkspaceSettings
        {
            AuthorizedPath = @"C:\Proyectos\Kohana",
            AutonomyLevel = WorkspaceAutonomyLevel.AutomatizarSecuencia
        };

        settings.Normalize();

        Assert.Equal(WorkspaceAutonomyPolicy.Default, settings.AutonomyLevel);
    }

    [Fact]
    public void Revoking_LeavesNoTraceOfAccess()
    {
        var settings = new WorkspaceSettings
        {
            AuthorizedPath = @"C:\Proyectos\Kohana",
            AuthorizedAt = DateTimeOffset.Now
        };

        settings.Revoke();
        settings.Normalize();

        Assert.False(settings.HasAuthorizedFolder);
        Assert.Null(settings.AuthorizedAt);
    }

    // ---------- Contexto que sale del equipo ----------

    [Fact]
    public void TheProjectContext_CarriesStructure_AndSaysItCannotWrite()
    {
        var files = new[]
        {
            new WorkspaceFile(@"C:\Proyectos\Kohana\Program.cs", "Program.cs", 1200),
            new WorkspaceFile(@"C:\Proyectos\Kohana\src\App.cs", @"src\App.cs", 800)
        };

        var context = WorkspaceContextBuilder.BuildStructure(
            @"C:\Proyectos\Kohana", files, WorkspaceAutonomyLevel.Guiar);

        Assert.NotNull(context);
        Assert.Contains("Program.cs", context);
        Assert.Contains("No puedes modificar archivos", context);
    }

    [Fact]
    public void WithNoFiles_NoProjectContextIsSent() =>
        Assert.Null(WorkspaceContextBuilder.BuildStructure(
            @"C:\Proyectos\Kohana", [], WorkspaceAutonomyLevel.Guiar));

    [Fact]
    public void WhenSomethingWasRedacted_ItIsSaid()
    {
        // Un secreto tapado en silencio haría creer que no había ninguno.
        var files = new[]
        {
            WorkspaceReadResult.Read("appsettings.json", "\"ApiKey\": \"[SECRETO REDACTADO]\"", secretsRedacted: true)
        };

        var excerpts = WorkspaceContextBuilder.BuildFileExcerpts(files);

        Assert.Contains("se ocultó", excerpts);
    }

    [Fact]
    public void RefusedFiles_AreNotIncludedInTheExcerpts()
    {
        var files = new[]
        {
            WorkspaceReadResult.Refused(".env", "Parece un archivo de credenciales.")
        };

        Assert.Equal(string.Empty, WorkspaceContextBuilder.BuildFileExcerpts(files));
    }
}
