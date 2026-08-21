using Nexo.Core.Updates;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D62 — leer el anuncio de una versión sin fiarse de él.
///
/// Todo esto llega de la red. Un anuncio manipulado o simplemente equivocado acaba en que Sakura
/// descarga y ejecuta algo que nadie publicó, que es el peor fallo posible de un actualizador: no
/// rompe la aplicación, la sustituye.
/// </summary>
public sealed class UpdateManifestTests
{
    private const string GoodHash =
        "c4b2b97223b013944f30ad7f0ee529007a71251f64d920569a133e897bba2985";

    private static UpdateManifestResult Read(
        string version = "0.9.6-beta",
        string url = "https://github.com/EXOTARA/Nexo/releases/download/v0.9.6/Sakura.zip",
        string? hash = GoodHash,
        string size = "99700000",
        string notes = "Arreglos varios") =>
        UpdateManifestReader.Read(version, url, hash, size, notes);

    [Fact]
    public void AGoodAnnouncementIsAccepted()
    {
        var result = Read();

        Assert.True(result.IsUsable);
        Assert.Equal("0.9.6-beta", result.Manifest!.Version.ToString());
        Assert.Equal(GoodHash, result.Manifest.Sha256);
    }

    [Fact]
    public void PlainHttpIsRefused()
    {
        // Comprobar la huella después no salva nada si quien pudo cambiar el archivo también pudo
        // escribir la huella.
        var result = Read(url: "http://github.com/EXOTARA/Nexo/releases/download/v0.9.6/Sakura.zip");

        Assert.False(result.IsUsable);
        Assert.Contains("https", result.Problem, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc123")]
    [InlineData("zzzz2b97223b013944f30ad7f0ee529007a71251f64d920569a133e897bba2985")]
    public void AHashThatIsNotAHashIsRefused(string? hash) =>
        Assert.False(Read(hash: hash).IsUsable);

    [Fact]
    public void AnAnnouncementWithoutASizeIsRefused() =>
        Assert.False(Read(size: "").IsUsable);

    [Fact]
    public void AnAbsurdSizeIsRefused()
    {
        // Evita empezar a bajar tres gigas por un error de publicación.
        Assert.False(Read(size: "3000000000").IsUsable);
    }

    [Fact]
    public void AnUnreadableVersionIsRefused() =>
        Assert.False(Read(version: "la nueva").IsUsable);

    [Fact]
    public void FingerprintsCompareWithoutCaring_AboutCase()
    {
        // PowerShell las escribe en mayúsculas y sha256sum en minúsculas. Una versión rechazada por
        // eso sería un fallo imposible de entender desde fuera.
        Assert.True(UpdateManifestReader.MatchesFingerprint(GoodHash, GoodHash.ToUpperInvariant()));
    }

    [Fact]
    public void ADifferentFingerprintDoesNotMatch() =>
        Assert.False(UpdateManifestReader.MatchesFingerprint(GoodHash, new string('a', 64)));
}

/// <summary>
/// Diseño D62 — el orden de aplicación y qué se deshace en cada punto.
///
/// Lo que hace peligroso a un actualizador no es fallar —fallará— sino fallar a mitad, dejando
/// media instalación vieja y media nueva.
/// </summary>
public sealed class UpdateApplicationPlanTests
{
    [Fact]
    public void EverythingReversibleHappensBeforeAnythingIsTouched()
    {
        // La propiedad que hace seguro el plan: ningún paso que toca la instalación ocurre antes de
        // un paso que todavía puede fallar sin consecuencias.
        var steps = UpdateApplicationPlan.Steps;
        var firstTouch = steps
            .Select((step, index) => (step, index))
            .First(pair => UpdateApplicationPlan.TouchesInstallation(pair.step)).index;

        Assert.All(
            steps.Take(firstTouch),
            step => Assert.False(UpdateApplicationPlan.TouchesInstallation(step)));
    }

    [Fact]
    public void TheOldVersionIsDiscardedLast()
    {
        // Un paquete puede estar íntegro, desempaquetarse bien y aun así no arrancar en este equipo.
        // Si para entonces ya se borró lo anterior, no queda a dónde volver.
        Assert.Equal(UpdateStep.DiscardPrevious, UpdateApplicationPlan.Steps[^1]);
    }

    [Fact]
    public void FailingBeforeTouchingAnything_LeavesNothingToUndo() =>
        Assert.Equal(
            UpdateRecovery.NothingToUndo,
            UpdateApplicationPlan.RecoveryFor(UpdateStep.VerifyPackage));

    [Theory]
    [InlineData(UpdateStep.StageBesideInstall)]
    [InlineData(UpdateStep.VerifyStaged)]
    public void FailingWhileStaging_OnlyThrowsAwayTheStaging(UpdateStep step) =>
        Assert.Equal(UpdateRecovery.DiscardStaged, UpdateApplicationPlan.RecoveryFor(step));

    [Theory]
    [InlineData(UpdateStep.SetAsidePrevious)]
    [InlineData(UpdateStep.PromoteStaged)]
    public void FailingMidSwap_PutsTheOldVersionBack(UpdateStep step) =>
        Assert.Equal(UpdateRecovery.RestorePrevious, UpdateApplicationPlan.RecoveryFor(step));

    [Fact]
    public void FailingToDeleteTheOldVersion_IsNotWorthUndoing()
    {
        // Lo nuevo ya está puesto y funcionando. Volver atrás aquí sería desinstalar una versión que
        // va bien por no poder borrar una carpeta.
        Assert.Equal(
            UpdateRecovery.NothingToUndo,
            UpdateApplicationPlan.RecoveryFor(UpdateStep.DiscardPrevious));
    }

    [Fact]
    public void CancellingIsOnlyOfferedWhileItIsFree()
    {
        // Un botón de cancelar que sigue ahí cuando ya no se puede cancelar promete algo que no va a
        // cumplir.
        Assert.True(UpdateApplicationPlan.CanCancelAt(UpdateStep.StageBesideInstall));
        Assert.False(UpdateApplicationPlan.CanCancelAt(UpdateStep.PromoteStaged));
    }

    [Fact]
    public void EveryStepCanBeExplained() =>
        Assert.All(
            UpdateApplicationPlan.Steps,
            step => Assert.NotEmpty(UpdateApplicationPlan.Describe(step)));
}
