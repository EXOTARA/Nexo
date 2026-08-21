using Nexo.Core.Updates;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D63 — qué carpetas puede mover el actualizador.
///
/// Estas pruebas protegen contra pérdida, no contra un fallo. El intercambio **aparta una carpeta y
/// borra otra**; si la carpeta de instalación resultara ser la raíz de un disco o el escritorio de
/// alguien, el actualizador la apartaría entera y no hay reintento que arregle eso.
///
/// Es el mismo razonamiento que WorkspaceRootPolicy —que nació de autorizar C:\ y colgar la
/// aplicación— aplicado a algo bastante menos perdonable: allí se leía de más, aquí se movería de
/// más.
/// </summary>
public sealed class UpdateSwapPathPolicyTests
{
    [Fact]
    public void ARealInstallFolderIsAccepted()
    {
        var paths = UpdateSwapPathPolicy.Resolve(@"C:\Users\Alguien\AppData\Local\Programs\Sakura");

        Assert.True(paths.IsSafe, paths.Problem);
        Assert.EndsWith(@"\Sakura", paths.Install, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(@"\Sakura.new", paths.Staged, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(@"\Sakura.old", paths.Previous, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheThreeFoldersAreSiblingsOnOneVolume()
    {
        // Mover dentro de un volumen es instantáneo; entre volúmenes Windows lo convierte en copiar
        // y borrar, que con quinientos archivos puede cortarse a mitad — el estado que todo este
        // diseño evita.
        var paths = UpdateSwapPathPolicy.Resolve(@"C:\Users\Alguien\AppData\Local\Programs\Sakura");

        Assert.True(UpdateSwapPathPolicy.SameVolume(paths.Install, paths.Staged));
        Assert.True(UpdateSwapPathPolicy.SameVolume(paths.Install, paths.Previous));
    }

    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"C:")]
    [InlineData(@"D:\")]
    public void TheRootOfADriveIsRefused(string path)
    {
        // Apartar la raíz sería apartar todo lo demás.
        Assert.False(UpdateSwapPathPolicy.Resolve(path).IsSafe);
    }

    [Fact]
    public void AFolderDirectlyOffTheRootIsRefused()
    {
        // «C:\Sakura» está demasiado arriba: es el tipo de ruta que aparece por un ajuste heredado,
        // y el precio de rechazarla es cero.
        Assert.False(UpdateSwapPathPolicy.Resolve(@"C:\Sakura").IsSafe);
    }

    [Fact]
    public void TheUserProfileIsRefused() =>
        Assert.False(UpdateSwapPathPolicy.Resolve(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)).IsSafe);

    [Fact]
    public void TheDesktopIsRefused() =>
        Assert.False(UpdateSwapPathPolicy.Resolve(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)).IsSafe);

    [Fact]
    public void DocumentsIsRefused() =>
        Assert.False(UpdateSwapPathPolicy.Resolve(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)).IsSafe);

    [Fact]
    public void LocalAppDataItselfIsRefused()
    {
        // La instalación vive DENTRO de LocalAppData, así que la carpeta padre no puede confundirse
        // con la instalación: apartarla se llevaría los datos de todos los programas.
        Assert.False(UpdateSwapPathPolicy.Resolve(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)).IsSafe);
    }

    [Fact]
    public void WindowsAndProgramFilesAreRefused()
    {
        Assert.False(UpdateSwapPathPolicy.Resolve(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows)).IsSafe);
        Assert.False(UpdateSwapPathPolicy.Resolve(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)).IsSafe);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoFolderAtAllIsRefused(string? path) =>
        Assert.False(UpdateSwapPathPolicy.Resolve(path).IsSafe);

    [Fact]
    public void ATrailingSeparatorDoesNotChangeTheAnswer()
    {
        // Sin normalizar, «…\Sakura\» daría carpetas hermanas llamadas «…\Sakura\.new».
        var withSlash = UpdateSwapPathPolicy.Resolve(@"C:\Users\Alguien\AppData\Local\Programs\Sakura\");
        var without = UpdateSwapPathPolicy.Resolve(@"C:\Users\Alguien\AppData\Local\Programs\Sakura");

        Assert.True(withSlash.IsSafe);
        Assert.Equal(without.Install, withSlash.Install);
        Assert.Equal(without.Staged, withSlash.Staged);
    }

    [Fact]
    public void RefusalsSayWhy() =>
        Assert.NotEmpty(UpdateSwapPathPolicy.Resolve(@"C:\").Problem);
}
