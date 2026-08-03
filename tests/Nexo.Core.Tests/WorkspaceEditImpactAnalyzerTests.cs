using Nexo.Core.Workspace;

namespace Nexo.Core.Tests;

/// <summary>
/// Escrito a partir de un fallo real visto usando la app: tras un cambio aplicado, el usuario añadió
/// una línea suya al final del archivo; en la siguiente petición el modelo leyó el archivo, conservó
/// todo lo demás y devolvió el contenido sin esa línea. La salvaguarda existente solo comprueba que
/// el contenido actual VIAJÓ al modelo, no que el modelo lo conservara, así que el cambio se aplicó
/// y la línea se perdió tras una confirmación que solo decía "REEMPLAZAR" y el tamaño en caracteres.
/// </summary>
public sealed class WorkspaceEditImpactAnalyzerTests
{
    [Fact]
    public void ALineTheUserAddedAndTheModelDropped_IsReported()
    {
        var current = "using System;\n\nclass Programa\n{\n}\n\n// Linea escrita a mano por el usuario\n";
        var proposed = "using System;\n\nclass Programa\n{\n}\n";

        var impact = WorkspaceEditImpactAnalyzer.Compare(current, proposed);

        Assert.True(impact.RemovesContent);
        Assert.Equal("// Linea escrita a mano por el usuario", Assert.Single(impact.RemovedLines));

        var warning = WorkspaceEditImpactAnalyzer.DescribeRemovals(impact);
        Assert.NotNull(warning);
        Assert.Contains("QUITA 1", warning);
        Assert.Contains("Linea escrita a mano por el usuario", warning);
    }

    [Fact]
    public void PurelyAddingALine_WarnsAboutNothing()
    {
        var impact = WorkspaceEditImpactAnalyzer.Compare(
            "using System;\n", "// revisado\nusing System;\n");

        Assert.False(impact.RemovesContent);
        Assert.Null(WorkspaceEditImpactAnalyzer.DescribeRemovals(impact));
    }

    [Fact]
    public void MovingALine_IsNotLosingIt()
    {
        // Un diff posicional gritaría aquí. Avisar de esto enseña a ignorar el aviso.
        var impact = WorkspaceEditImpactAnalyzer.Compare("a\nb\nc\n", "c\nb\na\n");

        Assert.False(impact.RemovesContent);
    }

    [Fact]
    public void BlankLineChanges_AreNotContentLoss()
    {
        var impact = WorkspaceEditImpactAnalyzer.Compare("a\n\n\nb\n", "a\nb\n");

        Assert.False(impact.RemovesContent);
    }

    [Fact]
    public void ARepeatedLineLosingOneCopy_CountsAsOneRemoval()
    {
        var impact = WorkspaceEditImpactAnalyzer.Compare("x\nx\nx\n", "x\nx\n");

        Assert.Equal(1, impact.RemovedLines.Count);
    }

    [Fact]
    public void WipingTheWholeFile_ReportsEveryLine()
    {
        // La forma exacta del incidente del README: el archivo entero sustituido por una línea.
        var current = "# Proyecto\n\nDescripcion larga.\nMas contenido.\nY mas.\n";
        var impact = WorkspaceEditImpactAnalyzer.Compare(current, "// comentario\n");

        Assert.True(impact.RemovesContent);
        Assert.Equal(4, impact.RemovedLines.Count);
        Assert.Contains("QUITA 4", WorkspaceEditImpactAnalyzer.DescribeRemovals(impact));
    }

    [Fact]
    public void ManyRemovals_AreSummarizedInsteadOfListedForever()
    {
        var current = string.Join("\n", Enumerable.Range(1, 40).Select(number => $"linea {number}"));
        var impact = WorkspaceEditImpactAnalyzer.Compare(current, "linea 1\n");

        var warning = WorkspaceEditImpactAnalyzer.DescribeRemovals(impact);

        Assert.Contains("QUITA 39", warning);
        Assert.Contains($"y {39 - WorkspaceEditImpactAnalyzer.MaximumListedRemovals} línea(s) más", warning);
    }

    [Fact]
    public void CreatingANewFile_HasNothingToLose()
    {
        var impact = WorkspaceEditImpactAnalyzer.Compare(null, "contenido nuevo\n");

        Assert.False(impact.RemovesContent);
        Assert.Equal(0, impact.LinesBefore);
    }
}
