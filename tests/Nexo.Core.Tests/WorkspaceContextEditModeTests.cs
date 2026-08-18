using Nexo.Core.Permissions;
using Nexo.Core.Workspace;

namespace Nexo.Core.Tests;

/// <summary>
/// Corrección de un defecto real (probado a mano): el contexto que acompañaba una petición de
/// cambio decía siempre "no puedes modificar archivos", incluso en el mismo mensaje donde se le
/// pedía al modelo un KOHANA-EDIT — dos instrucciones contradictorias en un mismo contexto. Y el
/// contenido actual del archivo destino nunca viajaba, así que el modelo solo podía inventar un
/// reemplazo completo. Estas pruebas cubren las dos piezas de la corrección.
/// </summary>
public sealed class WorkspaceContextEditModeTests
{
    private static readonly WorkspaceFile[] Files =
    [
        new(@"C:\Proyecto\README.md", "README.md", 100)
    ];

    [Fact]
    public void ExplainMode_StillSaysItCannotModifyFiles()
    {
        var context = WorkspaceContextBuilder.BuildStructure(
            @"C:\Proyecto", Files, AutonomyLevel.Guiar, editRequested: false);

        Assert.Contains("No puedes modificar archivos", context);
    }

    [Fact]
    public void EditMode_DoesNotContradictTheEditRequestItAccompanies()
    {
        // Antes, este mismo mensaje llevaba "no puedes modificar archivos" Y "usa el formato
        // KOHANA-EDIT" a la vez.
        var context = WorkspaceContextBuilder.BuildStructure(
            @"C:\Proyecto", Files, AutonomyLevel.EjecutarUnPaso, editRequested: true);

        Assert.DoesNotContain("No puedes modificar archivos", context);
        Assert.Contains("KOHANA-EDIT", context);
    }

    [Fact]
    public void PreservationNotice_NamesTheResolvedFiles()
    {
        var notice = WorkspaceContextBuilder.EditPreservationNotice(Files);

        Assert.Contains("README.md", notice);
        Assert.Contains("COMPLETO", notice);
    }

    [Fact]
    public void PreservationNotice_ForbidsStartingFromScratch()
    {
        var notice = WorkspaceContextBuilder.EditPreservationNotice(Files);

        // Es literalmente la instrucción que falló sin esto: el modelo trató la petición de cambio
        // como si pudiera empezar el archivo de cero.
        Assert.Contains("empiece de cero", notice);
    }

    [Fact]
    public void WithNoResolvedFiles_NoNoticeIsSent()
    {
        // Sin un archivo concreto que conservar, la instrucción no aporta nada y solo añade ruido.
        Assert.Equal(string.Empty, WorkspaceContextBuilder.EditPreservationNotice([]));
    }
}
