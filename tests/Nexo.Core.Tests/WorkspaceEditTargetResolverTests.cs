using Nexo.Core.Workspace;

namespace Nexo.Core.Tests;

/// <summary>
/// Corrección de un defecto real, encontrado probando D14 a mano: "agrega un comentario al README"
/// sustituyó el README entero por una sola línea, porque el modelo nunca vio el contenido actual del
/// archivo. Este resolver es la pieza que decide, sin IA, a qué archivos existentes se refiere una
/// instrucción, para poder leer su contenido de verdad antes de pedir el cambio.
/// </summary>
public sealed class WorkspaceEditTargetResolverTests
{
    private static WorkspaceFile File(string relativePath) =>
        new($@"C:\Proyecto\{relativePath}", relativePath, 100);

    private static readonly WorkspaceFile[] ProjectFiles =
    [
        File("README.md"),
        File("script.js"),
        File("styles.css"),
        File(@"src\App.cs")
    ];

    [Fact]
    public void MatchesByExactFileName()
    {
        var matches = WorkspaceEditTargetResolver.Resolve("actualiza README.md por favor", ProjectFiles);

        Assert.Single(matches, file => file.RelativePath == "README.md");
    }

    [Fact]
    public void MatchesCaseInsensitively()
    {
        var matches = WorkspaceEditTargetResolver.Resolve("actualiza readme.md", ProjectFiles);

        Assert.Contains(matches, file => file.RelativePath == "README.md");
    }

    [Fact]
    public void MatchesByNameWithoutExtension()
    {
        // "agrega un comentario al README" es exactamente la frase que causó el defecto real.
        var matches = WorkspaceEditTargetResolver.Resolve(
            "agrega un comentario al README", ProjectFiles);

        Assert.Contains(matches, file => file.RelativePath == "README.md");
    }

    [Fact]
    public void DoesNotMatchAShortNameAsASubstring()
    {
        // "app" (dentro de src\App.cs) no debe dispararse con cualquier aparición de esas tres
        // letras: exige límite de palabra completa.
        var matches = WorkspaceEditTargetResolver.Resolve(
            "quiero aprender a programar aplicaciones", ProjectFiles);

        Assert.DoesNotContain(matches, file => file.RelativePath == @"src\App.cs");
    }

    [Fact]
    public void MatchesByWholeWordEvenWithoutExtension()
    {
        var matches = WorkspaceEditTargetResolver.Resolve("modifica el script por favor", ProjectFiles);

        Assert.Contains(matches, file => file.RelativePath == "script.js");
    }

    [Fact]
    public void WithNoNameMentioned_ResolvesNothing()
    {
        // Ante la duda, no se incluye nada: adivinar aquí daría al modelo contenido que nadie pidió.
        var matches = WorkspaceEditTargetResolver.Resolve(
            "haz que el sitio se vea más bonito", ProjectFiles);

        Assert.Empty(matches);
    }

    [Fact]
    public void SeveralNamedFiles_AreAllResolved()
    {
        var matches = WorkspaceEditTargetResolver.Resolve(
            "actualiza README.md y script.js", ProjectFiles);

        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public void MatchesAreCappedEvenIfManyFilesAreNamed()
    {
        var manyFiles = Enumerable.Range(0, 10)
            .Select(index => File($"archivo{index}.txt"))
            .ToArray();

        var prompt = string.Join(" y ", manyFiles.Select(file => file.RelativePath));
        var matches = WorkspaceEditTargetResolver.Resolve(prompt, manyFiles);

        Assert.True(matches.Count <= WorkspaceEditTargetResolver.MaximumMatches);
    }

    [Fact]
    public void EmptyPromptOrNoFiles_ResolvesNothing()
    {
        Assert.Empty(WorkspaceEditTargetResolver.Resolve(null, ProjectFiles));
        Assert.Empty(WorkspaceEditTargetResolver.Resolve("README.md", []));
    }
}
