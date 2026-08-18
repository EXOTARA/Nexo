using Nexo.Core.Workspace;

namespace Nexo.Core.Tests;

/// <summary>
/// El contexto que ve el modelo empieza por "Proyecto autorizado: &lt;nombre de la carpeta&gt;" y
/// después lista los archivos relativos a esa carpeta, así que devolver
/// "&lt;carpeta&gt;/archivo.cs" es una lectura razonable del propio formato. Antes eso resolvía a una
/// ruta duplicada e inexistente, con dos consecuencias que no se ven leyendo el código: la
/// confirmación decía "Voy a CREAR" sobre un archivo existente, y la salvaguarda que exige haber
/// leído el contenido actual —la añadida tras el borrado del README— se saltaba entera, porque
/// empieza por <c>exists &amp;&amp;</c>.
/// </summary>
public sealed class WorkspaceRelativePathNormalizationTests : IDisposable
{
    private readonly string _root;

    public WorkspaceRelativePathNormalizationTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(), "kohana-workspace-path-tests", Guid.NewGuid().ToString("N"), "ProyectoPrueba");
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "Programa.cs"), "// original");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path.GetDirectoryName(_root)!, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Limpieza best-effort.
        }
    }

    [Theory]
    [InlineData("ProyectoPrueba/Programa.cs")]
    [InlineData("ProyectoPrueba\\Programa.cs")]
    [InlineData("proyectoprueba/Programa.cs")]
    public void APathPrefixedWithTheProjectFolder_ResolvesToTheRealFile(string written)
    {
        var resolved = WorkspacePathPolicy.ResolveFullPath(_root, written);

        Assert.Equal(Path.Combine(_root, "Programa.cs"), resolved);
        Assert.True(File.Exists(resolved), "El archivo existente tiene que reconocerse como existente.");
    }

    [Fact]
    public void APlainRelativePath_IsLeftAlone()
    {
        Assert.Equal("Programa.cs", WorkspacePathPolicy.NormalizeRelativePath(_root, "Programa.cs"));
    }

    [Fact]
    public void APrefixThatDoesNotLeadToARealFile_IsRespectedNotGuessedAway()
    {
        // Si el proyecto sí tuviera una subcarpeta con su mismo nombre, quitar el prefijo apuntaría
        // al archivo equivocado. Solo se quita cuando al quitarlo aparece un archivo de verdad.
        Assert.Equal(
            Path.Combine("ProyectoPrueba", "NoExiste.cs"),
            Path.Combine(WorkspacePathPolicy.NormalizeRelativePath(_root, "ProyectoPrueba/NoExiste.cs")
                .Split('/', '\\')));
    }

    [Fact]
    public void ARealSubfolderSharingTheProjectName_IsStillReachable()
    {
        var nested = Path.Combine(_root, "ProyectoPrueba");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "Programa.cs"), "// anidado");

        // Con una subcarpeta homónima real, el prefijo sí apunta a un archivo existente y se quita;
        // para llegar al anidado hay que nombrarla dos veces, que es lo que describe la ruta real.
        var resolved = WorkspacePathPolicy.ResolveFullPath(
            _root, "ProyectoPrueba/ProyectoPrueba/Programa.cs");

        Assert.Equal(Path.Combine(nested, "Programa.cs"), resolved);
    }
}
