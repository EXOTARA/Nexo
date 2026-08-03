using Nexo.Core.SelfCheck;
using Nexo.Windows.SelfCheck;

namespace Nexo.Windows.Tests;

/// <summary>
/// Ejecuta el Diseño D22 de verdad — sobre disco real, con DPAPI real — en vez de solo probar la
/// lógica pura. Es lo más cerca que una prueba automatizada puede llegar del comando «Comprobar que
/// Kohana funciona bien» sin abrir la interfaz: si algo en la máquina de compilación no puede
/// escribir, cifrar o verificar, esto lo dice aquí y no en el equipo de alguien.
/// </summary>
public sealed class SelfCheckEndToEndTests
{
    [Fact]
    public void EveryDiskCheckPasses_OnThisRealMachine()
    {
        var results = new WindowsSelfCheckProbes().Run();

        Assert.NotEmpty(results);

        var failed = results.Where(result => result.Status == SelfCheckStatus.Fallo).ToArray();
        Assert.True(failed.Length == 0,
            "Fallaron: " + string.Join(" | ", failed.Select(result => $"{result.Title}: {result.Detail}")));
    }

    [Fact]
    public void MemoryIsGenuinelyEncrypted_NotJustClaimedToBe()
    {
        var result = new WindowsSelfCheckProbes().Run()
            .Single(result => result.Id == "disco.memoria");

        Assert.Equal(SelfCheckStatus.Correcto, result.Status);
        Assert.Contains("cifrado", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheFullReport_ReadsCleanlyEndToEnd()
    {
        var logic = SelfCheckSuite.RunLogicChecks();
        var disk = new WindowsSelfCheckProbes().Run();

        var report = SelfCheckReport.Build([.. logic, .. disk]);

        Assert.Contains("todas funcionan en este equipo", report);
        Assert.Contains("Qué NO comprueba esto", report);
    }
}
