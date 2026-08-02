using Nexo.Core.Permissions;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D16 — el Permission Broker es el prerrequisito que el roadmap nombra para la Fase 7. Lo
/// que se prueba aquí es el ORDEN de sus comprobaciones, porque en un broker el orden no es un
/// detalle de implementación: es la política.
/// </summary>
public sealed class PermissionBrokerTests
{
    private static PermissionSettings Settings(
        KohanaCapability capability,
        PermissionLevel level,
        params string[] excludedApps)
    {
        var settings = new PermissionSettings();
        settings.Normalize();

        var permission = settings.For(capability);
        permission.Level = level;
        permission.ExcludedApps = [.. excludedApps];

        return settings;
    }

    private static PermissionRequest Request(
        KohanaCapability capability = KohanaCapability.Lens,
        string? targetApp = null,
        params MandatoryConfirmation[] categories) =>
        new(capability, "Hacer algo", targetApp, categories);

    // ---------- Valores por omisión ----------

    [Fact]
    public void ComputerUse_ArrivesBlocked()
    {
        // Es el permiso más alto del roadmap: no se concede por instalar Kohana.
        var settings = new PermissionSettings();
        settings.Normalize();

        Assert.Equal(PermissionLevel.Bloqueado, settings.For(KohanaCapability.ComputerUse).Level);
    }

    [Fact]
    public void EverythingElse_ArrivesAsAsk()
    {
        var settings = new PermissionSettings();
        settings.Normalize();

        Assert.All(
            Enum.GetValues<KohanaCapability>().Where(capability => capability != KohanaCapability.ComputerUse),
            capability => Assert.Equal(PermissionLevel.Preguntar, settings.For(capability).Level));
    }

    [Fact]
    public void EveryCapabilityHasItsOwnPermission()
    {
        // "Habilitar Lens no habilita Computer Use."
        var settings = Settings(KohanaCapability.Lens, PermissionLevel.Permitido);

        Assert.Equal(PermissionOutcome.Permitido,
            PermissionBroker.Decide(Request(KohanaCapability.Lens), settings).Outcome);
        Assert.Equal(PermissionOutcome.Denegado,
            PermissionBroker.Decide(Request(KohanaCapability.ComputerUse), settings).Outcome);
    }

    // ---------- El orden de las comprobaciones ----------

    [Fact]
    public void AnAppExclusion_BeatsEvenAnAllowedCapability()
    {
        // El modelo de confianza dice que una exclusión vale "incluso si la capacidad en general
        // está habilitada", así que tiene que ganarle a Permitido.
        var settings = Settings(KohanaCapability.Lens, PermissionLevel.Permitido, "banco");

        var decision = PermissionBroker.Decide(
            Request(KohanaCapability.Lens, targetApp: "Banco - Google Chrome"), settings);

        Assert.Equal(PermissionOutcome.Denegado, decision.Outcome);
        Assert.Contains("banco", decision.Reason);
    }

    [Fact]
    public void ExclusionsMatchByContent_NotByExactName() =>
        Assert.True(PermissionBroker.Decide(
            Request(KohanaCapability.Lens, targetApp: "Mi Banco Online — Firefox"),
            Settings(KohanaCapability.Lens, PermissionLevel.Permitido, "banco")).IsDenied);

    [Fact]
    public void ABlockedCapability_IsDenied_NotAsked()
    {
        var decision = PermissionBroker.Decide(
            Request(KohanaCapability.Flow), Settings(KohanaCapability.Flow, PermissionLevel.Bloqueado));

        Assert.Equal(PermissionOutcome.Denegado, decision.Outcome);
    }

    // ---------- Confirmaciones obligatorias ----------

    [Theory]
    [InlineData(MandatoryConfirmation.DatosSensibles)]
    [InlineData(MandatoryConfirmation.Credenciales)]
    [InlineData(MandatoryConfirmation.Pagos)]
    [InlineData(MandatoryConfirmation.BorradoIrreversible)]
    [InlineData(MandatoryConfirmation.ElevacionAdministrativa)]
    [InlineData(MandatoryConfirmation.PublicacionExterna)]
    [InlineData(MandatoryConfirmation.ComandosDeSistemaAmplios)]
    public void MandatoryCategories_AlwaysAsk_EvenWhenAllowed(MandatoryConfirmation category)
    {
        // "Sin importar el nivel de autonomía configurado." Estar en Permitido no las salta; ése es
        // el punto de que existan.
        var settings = Settings(KohanaCapability.Proyecto, PermissionLevel.Permitido);

        var decision = PermissionBroker.Decide(
            Request(KohanaCapability.Proyecto, null, category), settings);

        Assert.Equal(PermissionOutcome.RequiereConfirmacion, decision.Outcome);
        Assert.Contains(category, decision.TriggeredCategories);
    }

    [Fact]
    public void ABlockedCapability_IsStillDenied_EvenWithAMandatoryCategory()
    {
        // Denegar gana a preguntar: pedir confirmación de algo que de todas formas no se va a hacer
        // sería enseñar a aceptar por costumbre.
        var settings = Settings(KohanaCapability.ComputerUse, PermissionLevel.Bloqueado);

        Assert.True(PermissionBroker.Decide(
            Request(KohanaCapability.ComputerUse, null, MandatoryConfirmation.Pagos), settings).IsDenied);
    }

    [Fact]
    public void TheReasonSaysWhyItIsBeingAsked()
    {
        var decision = PermissionBroker.Decide(
            Request(KohanaCapability.Memoria, null, MandatoryConfirmation.BorradoIrreversible),
            Settings(KohanaCapability.Memoria, PermissionLevel.Permitido));

        Assert.Contains("no se puede recuperar", decision.Reason);
    }

    // ---------- Camino normal ----------

    [Fact]
    public void AnAllowedCapabilityWithNothingSpecial_ProceedsWithoutAsking() =>
        Assert.True(PermissionBroker.Decide(
            Request(KohanaCapability.Lens),
            Settings(KohanaCapability.Lens, PermissionLevel.Permitido)).MayProceedWithoutAsking);

    [Fact]
    public void AskLevel_Asks() =>
        Assert.Equal(
            PermissionOutcome.RequiereConfirmacion,
            PermissionBroker.Decide(
                Request(KohanaCapability.Lens),
                Settings(KohanaCapability.Lens, PermissionLevel.Preguntar)).Outcome);

    [Fact]
    public void AnUnknownLevel_FailsClosed()
    {
        // Un nivel que no se reconoce no se interpreta como permiso.
        var settings = Settings(KohanaCapability.Lens, (PermissionLevel)99);

        Assert.False(PermissionBroker.Decide(Request(KohanaCapability.Lens), settings)
            .MayProceedWithoutAsking);
    }

    // ---------- Mínimo privilegio ----------

    [Fact]
    public void WideningAPermission_NeedsANewConfirmation()
    {
        Assert.True(PermissionBroker.RequiresNewConfirmation(
            PermissionLevel.Preguntar, PermissionLevel.Permitido));
        Assert.True(PermissionBroker.RequiresNewConfirmation(
            PermissionLevel.Bloqueado, PermissionLevel.Preguntar));
    }

    [Fact]
    public void RestrictingAPermission_DoesNot()
    {
        Assert.False(PermissionBroker.RequiresNewConfirmation(
            PermissionLevel.Permitido, PermissionLevel.Bloqueado));
        Assert.False(PermissionBroker.RequiresNewConfirmation(
            PermissionLevel.Preguntar, PermissionLevel.Preguntar));
    }

    // ---------- Archivo de ajustes escrito a mano ----------

    [Fact]
    public void DuplicatedEntries_ResolveToTheMostRestrictive()
    {
        // Resolverlo "por la última que aparezca" haría que añadir una línea al final ampliara
        // permisos sin que nadie lo confirmara.
        var settings = new PermissionSettings
        {
            Capabilities =
            [
                new CapabilityPermission { Capability = KohanaCapability.Lens, Level = PermissionLevel.Permitido },
                new CapabilityPermission { Capability = KohanaCapability.Lens, Level = PermissionLevel.Bloqueado }
            ]
        };

        settings.Normalize();

        Assert.Equal(PermissionLevel.Bloqueado, settings.For(KohanaCapability.Lens).Level);
    }

    [Fact]
    public void DuplicatedEntries_KeepEveryExclusion()
    {
        var settings = new PermissionSettings
        {
            Capabilities =
            [
                new CapabilityPermission
                {
                    Capability = KohanaCapability.Lens,
                    Level = PermissionLevel.Preguntar,
                    ExcludedApps = ["banco"]
                },
                new CapabilityPermission
                {
                    Capability = KohanaCapability.Lens,
                    Level = PermissionLevel.Permitido,
                    ExcludedApps = ["gestor de contraseñas"]
                }
            ]
        };

        settings.Normalize();

        var permission = settings.For(KohanaCapability.Lens);
        Assert.Equal(2, permission.ExcludedApps.Count);
    }

    [Fact]
    public void MissingCapabilities_AreRebuiltWithTheConservativeValue()
    {
        var settings = new PermissionSettings();
        settings.Normalize();

        Assert.Equal(Enum.GetValues<KohanaCapability>().Length, settings.Capabilities.Count);
        Assert.Equal(PermissionLevel.Bloqueado, settings.For(KohanaCapability.ComputerUse).Level);
    }
}
