using Nexo.Core.Workspace;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D12 (Fase 5) — el roadmap dice que el acceso es "a un workspace explícitamente
/// autorizado, nunca a todo el disco". Esta clase es lo que hace que esa frase signifique algo, así
/// que casi todo lo que se prueba aquí es lo que Sakura se NIEGA a leer.
/// </summary>
public sealed class WorkspacePathPolicyTests
{
    private static readonly string Root =
        Path.Combine(Path.GetTempPath(), "kohana-workspace-tests", "Proyecto");

    private static string InRoot(params string[] parts) =>
        Path.Combine([Root, .. parts]);

    // ---------- Sin autorización no se lee nada ----------

    [Fact]
    public void WithNoAuthorizedFolder_NothingCanBeRead()
    {
        var verdict = WorkspacePathPolicy.CanRead(InRoot("Program.cs"), authorizedRoot: null);

        Assert.False(verdict.IsAllowed);
        Assert.Equal(WorkspaceAccessDenial.SinAutorizacion, verdict.Denial);
    }

    // ---------- Contención ----------

    [Fact]
    public void AFileInsideTheAuthorizedFolder_CanBeRead()
    {
        var verdict = WorkspacePathPolicy.CanRead(InRoot("src", "Program.cs"), Root);

        Assert.True(verdict.IsAllowed);
    }

    [Fact]
    public void EscapingWithDotDot_IsCaught()
    {
        // La comprobación es sobre rutas ya resueltas: una comparación textual dejaría pasar esto.
        var escape = Path.Combine(Root, "..", "..", "Windows", "System32", "drivers.txt");

        Assert.False(WorkspacePathPolicy.IsInside(escape, Root));
        Assert.Equal(
            WorkspaceAccessDenial.FueraDelWorkspace,
            WorkspacePathPolicy.CanRead(escape, Root).Denial);
    }

    [Fact]
    public void ASiblingFolderWithTheSamePrefix_IsNotInside()
    {
        // "C:\...\Proyecto" no contiene a "C:\...\Proyecto-privado", aunque el prefijo coincida.
        var sibling = Root + "-privado" + Path.DirectorySeparatorChar + "notas.md";

        Assert.False(WorkspacePathPolicy.IsInside(sibling, Root));
    }

    [Fact]
    public void TheRootItself_CountsAsInside() =>
        Assert.True(WorkspacePathPolicy.IsInside(Root, Root));

    // ---------- Archivos de secretos ----------

    [Theory]
    [InlineData(".env")]
    [InlineData(".env.production")]
    [InlineData("id_rsa")]
    [InlineData("servidor.pem")]
    [InlineData("certificado.pfx")]
    [InlineData("secrets.json")]
    public void SecretFiles_AreRefusedByName_BeforeBeingOpened(string fileName)
    {
        // Se niegan por NOMBRE: leer el contenido para decidir si tiene secretos ya sería haberlo
        // leído.
        Assert.True(WorkspacePathPolicy.IsSecretFile(fileName));

        var verdict = WorkspacePathPolicy.CanRead(InRoot(fileName), Root);
        Assert.Equal(WorkspaceAccessDenial.ArchivoDeSecretos, verdict.Denial);
    }

    [Fact]
    public void AnOrdinaryJsonFile_IsNotTreatedAsASecret() =>
        Assert.False(WorkspacePathPolicy.IsSecretFile("appsettings.json"));

    // ---------- Carpetas excluidas ----------

    [Theory]
    [InlineData("node_modules")]
    [InlineData(".git")]
    [InlineData("bin")]
    [InlineData("obj")]
    public void DependencyAndBuildFolders_AreNotYourCode(string folder)
    {
        var verdict = WorkspacePathPolicy.CanRead(InRoot(folder, "algo.js"), Root);

        Assert.Equal(WorkspaceAccessDenial.CarpetaExcluida, verdict.Denial);
    }

    [Fact]
    public void AnExcludedNameInTheRootItself_DoesNotBlockTheWholeProject()
    {
        // Si alguien autoriza una carpeta llamada "build", su proyecto se llama build: negarse a
        // leerlo entero sería absurdo.
        var buildRoot = Path.Combine(Path.GetTempPath(), "kohana-workspace-tests", "build");

        Assert.True(WorkspacePathPolicy.CanRead(Path.Combine(buildRoot, "main.py"), buildRoot).IsAllowed);
    }

    // ---------- Lista de permitidos ----------

    [Theory]
    [InlineData("Program.cs")]
    [InlineData("app.ts")]
    [InlineData("README.md")]
    [InlineData("build.yml")]
    public void KnownProjectTextFiles_AreReadable(string fileName) =>
        Assert.True(WorkspacePathPolicy.CanRead(InRoot(fileName), Root).IsAllowed);

    [Theory]
    [InlineData("foto.png")]
    [InlineData("video.mp4")]
    [InlineData("programa.exe")]
    [InlineData("sinextension")]
    public void AnythingNotOnTheAllowList_IsRefused(string fileName)
    {
        // Lista de PERMITIDOS, no de prohibidos: con una lista de prohibidos, cada extensión nueva
        // del mundo entraría por defecto.
        var verdict = WorkspacePathPolicy.CanRead(InRoot(fileName), Root);

        Assert.Equal(WorkspaceAccessDenial.TipoNoSoportado, verdict.Denial);
    }

    // ---------- Tamaño ----------

    [Fact]
    public void AFileTooLargeToBeCode_IsNotReadWhole()
    {
        var verdict = WorkspacePathPolicy.CheckSize(WorkspacePathPolicy.MaximumFileBytes + 1);

        Assert.Equal(WorkspaceAccessDenial.DemasiadoGrande, verdict.Denial);
    }

    [Fact]
    public void AnOrdinarySizedFile_PassesTheSizeCheck() =>
        Assert.True(WorkspacePathPolicy.CheckSize(12_000).IsAllowed);

    // ---------- Corrección de un defecto real: comparar rutas con distinto separador ----------

    /// <summary>
    /// Este es exactamente el caso que rompía la salvaguarda contra sobrescribir un archivo sin
    /// haber leído su contenido: la plantilla que ve el modelo escribe el ejemplo con "/"
    /// ("ruta/relativa/del/archivo"), así que un modelo pidiendo cambiar un archivo dentro de una
    /// subcarpeta responde con "src/App.cs" aunque el lector, con <c>Path.GetRelativePath</c> en
    /// Windows, lo hubiera registrado como "src\App.cs". Sin resolver ambas al mismo camino
    /// canónico, la comparación de texto plano las trataría como archivos distintos.
    /// </summary>
    [Fact]
    public void ResolveFullPath_TreatsForwardAndBackSlashAsTheSameFile()
    {
        var withForwardSlash = WorkspacePathPolicy.ResolveFullPath(Root, "src/App.cs");
        var withBackSlash = WorkspacePathPolicy.ResolveFullPath(Root, @"src\App.cs");

        Assert.Equal(withForwardSlash, withBackSlash, ignoreCase: true);
    }

    [Fact]
    public void ResolveFullPath_TreatsCaseDifferencesAsTheSameFile()
    {
        // Path.GetFullPath no cambia mayúsculas por sí solo; la comparación en el sitio de uso es
        // la que debe hacerse sin distinguirlas (ver knownContentPaths en MainWindow), pero esta
        // prueba deja constancia de qué produce esta función para que ese contrato no se rompa
        // calladamente si alguien la cambia.
        var lower = WorkspacePathPolicy.ResolveFullPath(Root, "readme.md");
        var upper = WorkspacePathPolicy.ResolveFullPath(Root, "README.md");

        Assert.Equal(lower, upper, ignoreCase: true);
    }

    [Fact]
    public void ResolveFullPath_MatchesWhatTheFileSystemReaderWouldRecordAsRelativePath()
    {
        // El archivo en la raíz (sin subcarpeta) es el caso que el defecto original SÍ cubría por
        // casualidad, porque no hay separador de por medio. Esta prueba fija ese caso simple para
        // que una regresión futura en cualquiera de los dos lados se note aquí.
        var fromModelText = WorkspacePathPolicy.ResolveFullPath(Root, "README.md");
        var fromDiskWalk = Path.GetFullPath(Path.Combine(Root, Path.GetRelativePath(Root, InRoot("README.md"))));

        Assert.Equal(fromDiskWalk, fromModelText, ignoreCase: true);
    }
}
