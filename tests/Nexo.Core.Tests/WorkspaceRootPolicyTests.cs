using Nexo.Core.Workspace;
using Xunit;

namespace Nexo.Core.Tests;

/// <summary>
/// Regresión de un fallo real: se autorizó <c>C:\</c> como carpeta de proyecto y Kohana dejó de
/// responder. La lentitud era el síntoma; el problema es que un permiso pensado para acotar hasta
/// dónde llega Kohana no puede aceptar «todo el disco».
/// </summary>
public sealed class WorkspaceRootPolicyTests
{
    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"D:\")]
    [InlineData(@"C:\\")]
    public void TheRootOfADriveIsNotAProject(string path)
    {
        var verdict = WorkspaceRootPolicy.CanAuthorize(path);

        Assert.False(verdict.IsAllowed);
        Assert.Contains("unidad", verdict.Message);
    }

    [Fact]
    public void ADriveLetterWithoutASlashMeansTheCurrentFolderNotTheRoot()
    {
        // Rareza de Windows que conviene dejar por escrito: «C:» no es la raíz, es el directorio
        // actual de esa unidad, y Path.GetFullPath lo resuelve así. Se documenta para que nadie
        // «arregle» la política tratándolo como raíz — el selector de carpetas devuelve siempre
        // «C:\», y forzar lo contrario rechazaría la carpeta de trabajo de quien lo escriba a mano.
        //
        // La letra se toma del directorio actual y no se escribe «C:» a pelo. Windows guarda un
        // directorio actual **por unidad**, así que «C:» solo equivale al directorio actual cuando
        // el proceso ya está en C:. Con la C fija, esta prueba pasaba en el equipo de desarrollo y
        // fallaba en integración, que trabaja en D: — y allí «C:» devuelve «C:\».
        var current = Path.GetFullPath(Directory.GetCurrentDirectory());
        var bareDrive = Path.GetPathRoot(current)!.TrimEnd(Path.DirectorySeparatorChar);

        Assert.Equal(current, Path.GetFullPath(bareDrive));
    }

    [Fact]
    public void TheWholeUserProfileIsRefused()
    {
        // La trampa sutil: parece «mis cosas», pero incluye Descargas, Escritorio, Documentos y la
        // configuración de todos los programas instalados.
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Assert.False(WorkspaceRootPolicy.CanAuthorize(profile).IsAllowed);
    }

    [Fact]
    public void TheFolderThatHoldsEveryUserIsRefused()
    {
        var users = Path.GetDirectoryName(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        Assert.False(WorkspaceRootPolicy.CanAuthorize(users).IsAllowed);
    }

    [Fact]
    public void SystemFoldersAreRefused()
    {
        foreach (var special in new[]
                 {
                     Environment.SpecialFolder.Windows,
                     Environment.SpecialFolder.ProgramFiles,
                     Environment.SpecialFolder.CommonApplicationData
                 })
        {
            var folder = Environment.GetFolderPath(special);
            if (string.IsNullOrWhiteSpace(folder))
            {
                continue;
            }

            Assert.False(
                WorkspaceRootPolicy.CanAuthorize(folder).IsAllowed,
                $"{folder} debería rechazarse.");
        }
    }

    [Fact]
    public void SomethingInsideASystemFolderIsAlsoRefused()
    {
        var inside = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "drivers");

        Assert.False(WorkspaceRootPolicy.CanAuthorize(inside).IsAllowed);
    }

    [Fact]
    public void AFolderThatMerelyStartsLikeASystemOneIsAllowed()
    {
        // «C:\Windows-mis-cosas» no está dentro de «C:\Windows». Comparar por prefijo sin exigir el
        // separador rechazaría carpetas legítimas por parecerse de nombre.
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        Assert.True(WorkspaceRootPolicy.CanAuthorize(windows + "-mis-cosas").IsAllowed);
    }

    [Theory]
    [InlineData(@"C:\Dev\Nexo")]
    [InlineData(@"D:\proyectos\pagina")]
    public void ANormalProjectFolderIsAllowed(string path)
    {
        Assert.True(WorkspaceRootPolicy.CanAuthorize(path).IsAllowed);
    }

    [Fact]
    public void AFolderInsideTheProfileIsAllowed()
    {
        // Se rechaza el perfil entero, no lo que hay dentro: un proyecto en Documentos o en el
        // Escritorio es de lo más normal.
        var project = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop", "mi-web");

        Assert.True(WorkspaceRootPolicy.CanAuthorize(project).IsAllowed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NothingChosenIsRefusedWithoutThrowing(string? path)
    {
        var verdict = WorkspaceRootPolicy.CanAuthorize(path);

        Assert.False(verdict.IsAllowed);
        Assert.False(string.IsNullOrWhiteSpace(verdict.Message));
    }

    [Fact]
    public void ARefusalAlwaysExplainsWhy()
    {
        // El mensaje es lo que convierte un «no» en algo accionable: sin él, quien lo recibe solo
        // sabe que no funcionó.
        var verdict = WorkspaceRootPolicy.CanAuthorize(@"C:\");

        Assert.False(string.IsNullOrWhiteSpace(verdict.Message));
        Assert.Contains("carpeta", verdict.Message);
    }
}
