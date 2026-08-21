using Nexo.Core.Permissions;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D19 — las exclusiones por aplicación, editables sin abrir settings.json. Una exclusión que
/// la persona cree puesta y no lo está es una protección que no existe, así que el parser tiene que
/// ser claro sobre qué aceptó y qué no.
/// </summary>
public sealed class PermissionExclusionParserTests
{
    private static PermissionSettings Settings()
    {
        var settings = new PermissionSettings();
        settings.Normalize();
        return settings;
    }

    [Fact]
    public void AWellFormedLine_IsParsed()
    {
        var outcome = PermissionExclusionParser.Parse(["Lens: bitwarden"]);

        var exclusion = Assert.Single(outcome.Exclusions);
        Assert.Equal(SakuraCapability.Lens, exclusion.Capability);
        Assert.Equal("bitwarden", exclusion.App);
        Assert.Equal(0, outcome.IgnoredLines);
    }

    [Theory]
    [InlineData("lens: bitwarden")]
    [InlineData("LENS:bitwarden")]
    [InlineData("  Lens  :  bitwarden  ")]
    public void CaseAndSpacesDoNotMatter(string line) =>
        Assert.Single(PermissionExclusionParser.Parse([line]).Exclusions);

    [Theory]
    [InlineData("bitwarden")]
    [InlineData("Inventada: algo")]
    [InlineData("Lens:")]
    [InlineData(": bitwarden")]
    public void MalformedLines_AreIgnored_ButCounted(string line)
    {
        // Mismo criterio que el diccionario de Flow: una línea que no hace nada y no avisa parece
        // que la función está rota.
        var outcome = PermissionExclusionParser.Parse([line]);

        Assert.Empty(outcome.Exclusions);
        Assert.Equal(1, outcome.IgnoredLines);
    }

    [Fact]
    public void OneBadLineDoesNotSpoilTheRest()
    {
        var outcome = PermissionExclusionParser.Parse(
            ["Lens: bitwarden", "esto está mal", "ComputerUse: banco"]);

        Assert.Equal(2, outcome.Exclusions.Count);
        Assert.Equal(1, outcome.IgnoredLines);
    }

    [Fact]
    public void BlankLines_AreNeitherParsedNorCountedAsErrors()
    {
        var outcome = PermissionExclusionParser.Parse(["", "   ", "Lens: bitwarden"]);

        Assert.Single(outcome.Exclusions);
        Assert.Equal(0, outcome.IgnoredLines);
    }

    // ---------- Aplicar ----------

    [Fact]
    public void ApplyingReplaces_SoRemovingALineRemovesTheExclusion()
    {
        // Si se acumularan, quitar una exclusión desde la interfaz sería imposible.
        var settings = Settings();
        PermissionExclusionParser.Apply(settings, ["Lens: bitwarden", "Lens: banco"]);
        Assert.Equal(2, settings.For(SakuraCapability.Lens).ExcludedApps.Count);

        PermissionExclusionParser.Apply(settings, ["Lens: bitwarden"]);
        Assert.Equal(["bitwarden"], settings.For(SakuraCapability.Lens).ExcludedApps);
    }

    [Fact]
    public void ApplyingAnEmptyList_ClearsEverything()
    {
        var settings = Settings();
        PermissionExclusionParser.Apply(settings, ["Lens: bitwarden"]);

        PermissionExclusionParser.Apply(settings, []);

        Assert.All(settings.Capabilities, permission => Assert.Empty(permission.ExcludedApps));
    }

    [Fact]
    public void DuplicatesInTheSameCapability_AreCollapsed()
    {
        var settings = Settings();

        PermissionExclusionParser.Apply(settings, ["Lens: bitwarden", "Lens: BITWARDEN"]);

        Assert.Single(settings.For(SakuraCapability.Lens).ExcludedApps);
    }

    [Fact]
    public void AnExclusionAppliedThroughTheParser_ActuallyBlocksTheBroker()
    {
        // Lo que importa de verdad: que la línea escrita en la interfaz llegue a negar la acción.
        var settings = Settings();
        settings.For(SakuraCapability.Lens).Level = PermissionLevel.Permitido;

        PermissionExclusionParser.Apply(settings, ["Lens: bitwarden"]);

        var decision = PermissionBroker.Decide(
            new PermissionRequest(SakuraCapability.Lens, "Leer la ventana", "Bitwarden - Chrome"),
            settings);

        Assert.True(decision.IsDenied);
    }

    // ---------- Volcar ----------

    [Fact]
    public void FormatRoundTripsWhatWasApplied()
    {
        var settings = Settings();
        PermissionExclusionParser.Apply(settings, ["ComputerUse: banco", "Lens: bitwarden"]);

        var lines = PermissionExclusionParser.Format(settings);

        Assert.Equal(2, lines.Count);
        Assert.DoesNotContain(
            PermissionExclusionParser.Parse(lines).Exclusions,
            exclusion => string.IsNullOrWhiteSpace(exclusion.App));
    }

    [Fact]
    public void FormatIsStable_SoSavingDoesNotLookLikeAChange()
    {
        var settings = Settings();
        PermissionExclusionParser.Apply(settings, ["Lens: zeta", "Lens: alfa"]);

        Assert.Equal(
            PermissionExclusionParser.Format(settings),
            PermissionExclusionParser.Format(settings));
    }
}
