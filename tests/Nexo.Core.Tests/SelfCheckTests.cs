using Nexo.Core.SelfCheck;
using Nexo.Core.Settings;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D22 — pruebas de la autocomprobación. Suena circular (probar lo que prueba) y no lo es:
/// aquí se comprueba que **el detector detecta**, es decir, que una garantía rota se vería en el
/// informe en vez de pasar por verde. Un comprobador que siempre dice que sí es peor que ninguno,
/// porque además da confianza.
/// </summary>
public sealed class SelfCheckTests
{
    private static SelfCheckResult Find(string id) =>
        SelfCheckSuite.RunLogicChecks().Single(result => result.Id == id);

    [Fact]
    public void OnAHealthyBuild_EverythingPasses()
    {
        var results = SelfCheckSuite.RunLogicChecks();

        Assert.NotEmpty(results);
        Assert.All(results, result => Assert.Equal(SelfCheckStatus.Correcto, result.Status));
    }

    [Fact]
    public void EveryCheckSaysWhatItProves()
    {
        // Una lista de verdes sin decir qué demuestra cada uno da una seguridad que no se ha ganado.
        Assert.All(SelfCheckSuite.RunLogicChecks(), result =>
        {
            Assert.False(string.IsNullOrWhiteSpace(result.Title));
            Assert.False(string.IsNullOrWhiteSpace(result.WhatItProves));
        });
    }

    [Fact]
    public void ChecksHaveUniqueIdentifiers()
    {
        var results = SelfCheckSuite.RunLogicChecks();

        Assert.Equal(results.Count, results.Select(result => result.Id).Distinct().Count());
    }

    // ---------- Que el detector detecta ----------

    [Fact]
    public void TheMigrationCheckReachesTheCurrentSchema()
    {
        var result = Find("migracion");

        Assert.Contains(ShellPreferences.CurrentSchemaVersion.ToString(), result.Detail);
    }

    [Fact]
    public void TheSchemaConstantMatchesWhatNormalizeProduces()
    {
        // El motivo de que la constante exista: si alguien añade un escalón y olvida subirla, esto
        // falla aquí y no en el equipo de alguien.
        var preferences = new ShellPreferences { SchemaVersion = 0 };
        preferences.Normalize();

        Assert.Equal(ShellPreferences.CurrentSchemaVersion, preferences.SchemaVersion);
    }

    [Fact]
    public void ThePermissionCheckWouldNoticeAPermissiveDefault()
    {
        // No se puede romper el valor por omisión desde una prueba, así que se comprueba que el
        // check mira lo que dice mirar: el nivel de Computer Use aparece en su promesa.
        var result = Find("permisos.omision");

        Assert.Contains("bloqueado", result.WhatItProves, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheMandatoryConfirmationCheckCoversAllSevenCategories() =>
        Assert.Contains("siete", Find("permisos.obligatorias").Detail);

    [Fact]
    public void TheSafeShellCheckCountsEveryCommand() =>
        Assert.Contains(
            Nexo.Core.ComputerUse.SafeShellCatalog.All.Count.ToString(),
            Find("equipo.comandos").Detail);

    [Fact]
    public void ThePackCheckCoversEveryPack() =>
        Assert.Contains(
            Nexo.Core.Skills.SkillPackCatalog.All.Count.ToString(),
            Find("packs.permisos").Detail);

    [Fact]
    public void TheInventoryCheckCountsEveryFile() =>
        Assert.Contains(
            Nexo.Core.Productization.SakuraDataInventory.All.Count.ToString(),
            Find("datos.inventario").Detail);

    // ---------- El informe ----------

    [Fact]
    public void TheReportPutsFailuresFirst()
    {
        var report = SelfCheckReport.Build(
        [
            SelfCheckResult.Ok("a", "Todo bien", "algo"),
            SelfCheckResult.Fail("b", "Esto está roto", "otra cosa", "detalle"),
        ]);

        Assert.True(report.IndexOf("Esto está roto", StringComparison.Ordinal) <
                    report.IndexOf("Todo bien", StringComparison.Ordinal));
    }

    [Fact]
    public void TheReportSaysHowManyFailed()
    {
        var report = SelfCheckReport.Build(
        [
            SelfCheckResult.Ok("a", "Bien", "algo"),
            SelfCheckResult.Fail("b", "Roto", "algo", "detalle")
        ]);

        Assert.Contains("1 NO funcionan", report);
    }

    [Fact]
    public void AllGreenDoesNotClaimEverythingIsFine()
    {
        // Es lo más importante del informe: un verde total invitaría a concluir "está todo bien", y
        // eso sería falso porque nada de esto mira la interfaz.
        var report = SelfCheckReport.Build([SelfCheckResult.Ok("a", "Bien", "algo")]);

        Assert.Contains("Qué NO comprueba esto", report);
        Assert.Contains("no lo sustituye", report);
    }

    [Fact]
    public void NotCheckedIsNotTheSameAsPassed()
    {
        var report = SelfCheckReport.Build(
        [
            SelfCheckResult.Skipped("a", "Sin comprobar", "algo", "no se pudo")
        ]);

        Assert.Contains("merecen un vistazo", report);
        Assert.DoesNotContain("todas funcionan", report);
    }

    [Fact]
    public void TheReportMentionsWhatOnlyAPersonCanCheck()
    {
        var report = SelfCheckReport.Build([SelfCheckResult.Ok("a", "Bien", "algo")]);

        Assert.Contains("interfaz", report);
        Assert.Contains("dictado", report);
    }
}
