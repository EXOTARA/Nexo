using Nexo.Core.Workspace;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D14 — el parser del cambio propuesto. La regla es "el formato es exacto o no hay cambio":
/// un parser tolerante que adivina rutas o recompone bloques a medias acabaría escribiendo en el
/// archivo equivocado, y ese error no lo ve nadie hasta que ya ocurrió.
/// </summary>
public sealed class WorkspaceEditParserTests
{
    private static string Answer(string body) => body.ReplaceLineEndings();

    [Fact]
    public void AWellFormedProposal_IsParsed()
    {
        var edit = WorkspaceEditParser.Parse(Answer("""
            Claro, lo cambio así:

            KOHANA-EDIT: src/Program.cs
            KOHANA-WHY: renombra el método para que se entienda
            ```
            public class Program { }
            ```
            """));

        Assert.NotNull(edit);
        Assert.Equal("src/Program.cs", edit!.RelativePath);
        Assert.Contains("renombra el método", edit.Description);
        Assert.Contains("public class Program", edit.NewContent);
    }

    [Fact]
    public void AnOrdinaryAnswer_ProposesNothing() =>
        Assert.Null(WorkspaceEditParser.Parse(
            "Yo cambiaría el nombre del método a CalcularTotal, en src/Program.cs."));

    [Fact]
    public void TwoProposalsInOneAnswer_MeanNoneIsApplied()
    {
        // El nivel 4 es "un único paso confirmado": ante dos cambios, no se aplica ninguno.
        Assert.Null(WorkspaceEditParser.Parse(Answer("""
            KOHANA-EDIT: src/A.cs
            KOHANA-WHY: uno
            ```
            contenido a
            ```
            KOHANA-EDIT: src/B.cs
            KOHANA-WHY: dos
            ```
            contenido b
            ```
            """)));
    }

    [Fact]
    public void AnUnclosedBlock_IsATruncatedAnswer_AndIsRefused()
    {
        // Escribir lo que llegó dejaría el archivo a medias.
        Assert.Null(WorkspaceEditParser.Parse(Answer("""
            KOHANA-EDIT: src/Program.cs
            KOHANA-WHY: algo
            ```
            public class Program {
            """)));
    }

    [Fact]
    public void AnEmptyBlock_WouldSilentlyEmptyTheFile_AndIsRefused()
    {
        Assert.Null(WorkspaceEditParser.Parse(Answer("""
            KOHANA-EDIT: src/Program.cs
            KOHANA-WHY: algo
            ```
            ```
            """)));
    }

    [Fact]
    public void AProposalWithNoPath_IsRefused()
    {
        Assert.Null(WorkspaceEditParser.Parse(Answer("""
            KOHANA-EDIT:
            KOHANA-WHY: algo
            ```
            contenido
            ```
            """)));
    }

    [Fact]
    public void AProposalWithNoBlock_IsRefused() =>
        Assert.Null(WorkspaceEditParser.Parse(Answer("""
            KOHANA-EDIT: src/Program.cs
            KOHANA-WHY: algo, pero no adjunto el contenido
            """)));

    [Fact]
    public void TheReasonIsOptional_ButTheMarkerIsNot()
    {
        // Sin el marcador de motivo no se acepta: un cambio sin explicación no puede enseñarse en
        // la confirmación.
        Assert.Null(WorkspaceEditParser.Parse(Answer("""
            KOHANA-EDIT: src/Program.cs
            ```
            contenido
            ```
            """)));

        var edit = WorkspaceEditParser.Parse(Answer("""
            KOHANA-EDIT: src/Program.cs
            KOHANA-WHY:
            ```
            contenido
            ```
            """));

        Assert.NotNull(edit);
        Assert.False(string.IsNullOrWhiteSpace(edit!.Description));
    }

    [Fact]
    public void NullOrEmpty_ProposesNothing()
    {
        Assert.Null(WorkspaceEditParser.Parse(null));
        Assert.Null(WorkspaceEditParser.Parse("   "));
    }

    [Fact]
    public void TheInstructionsGivenToTheModel_MatchTheFormatAccepted()
    {
        // El formato pedido y el formato aceptado no pueden separarse con el tiempo.
        Assert.Contains(WorkspaceEditParser.PathMarker, WorkspaceEditParser.ModelInstructions);
        Assert.Contains(WorkspaceEditParser.ReasonMarker, WorkspaceEditParser.ModelInstructions);
    }
}
