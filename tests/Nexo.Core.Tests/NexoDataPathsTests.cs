using Nexo.Core.Diagnostics;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D3.2 — comparte colección con <see cref="ProductIdentityTests"/> (que lee
/// <c>NexoDataPaths.RootDirectory</c>/<c>LegacyRootDirectory</c> asumiendo que no hay ningún
/// override activo): xUnit paraleliza por colección, así que sin este agrupamiento explícito esas
/// aserciones podrían leer un override que otra prueba de esta clase dejó activo a mitad de
/// ejecución. Cada prueba de aquí limpia el override antes y después (constructor + Dispose), y no
/// existe ningún otro punto de la suite que toque este estado global.
/// </summary>
[CollectionDefinition(Name)]
public sealed class NexoDataPathsCollection
{
    public const string Name = "NexoDataPaths";
}

[Collection(NexoDataPathsCollection.Name)]
public sealed class NexoDataPathsTests : IDisposable
{
    private const string EnvVar = NexoDataPaths.DataRootEnvironmentVariable;

    public NexoDataPathsTests() => ResetState();

    public void Dispose() => ResetState();

    private static void ResetState()
    {
        NexoDataPaths.UseExplicitDataRoot(null);
        Environment.SetEnvironmentVariable(EnvVar, null);
    }

    private static string CreateTempRootPath() =>
        Path.Combine(Path.GetTempPath(), "kohana-validation-tests-" + Guid.NewGuid().ToString("N"));

    private static string LocalApplicationData =>
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    // ---------- Sin override: producción sin cambios ----------

    [Fact]
    public void NoOverride_UsesTheRealProductionRoot()
    {
        Assert.False(NexoDataPaths.IsUsingOverrideRoot);
        Assert.Equal(
            Path.Combine(LocalApplicationData, "Sakura"),
            NexoDataPaths.RootDirectory);
    }

    [Fact]
    public void NoOverride_LegacyRootStaysTheRealNexoFolder_NotCollapsed()
    {
        Assert.Equal(
            Path.Combine(LocalApplicationData, "Kohana"),
            NexoDataPaths.LegacyRootDirectory);
        Assert.NotEqual(NexoDataPaths.RootDirectory, NexoDataPaths.LegacyRootDirectory);
    }

    [Fact]
    public void NoOverride_DescribeActiveRoot_ReportsProduction()
    {
        var (root, source) = NexoDataPaths.DescribeActiveRoot();

        Assert.Equal(NexoDataPaths.RootDirectory, root);
        Assert.Equal("producción", source);
    }

    // ---------- Override explícito ----------

    [Fact]
    public void ExplicitOverride_TakesEffectImmediately()
    {
        var overrideRoot = CreateTempRootPath();

        NexoDataPaths.UseExplicitDataRoot(overrideRoot);

        Assert.True(NexoDataPaths.IsUsingOverrideRoot);
        Assert.Equal(Path.GetFullPath(overrideRoot), NexoDataPaths.RootDirectory);
    }

    [Fact]
    public void ExplicitOverride_ClearedByNull_FallsBackToProduction()
    {
        NexoDataPaths.UseExplicitDataRoot(CreateTempRootPath());

        NexoDataPaths.UseExplicitDataRoot(null);

        Assert.False(NexoDataPaths.IsUsingOverrideRoot);
        Assert.Equal(Path.Combine(LocalApplicationData, "Sakura"), NexoDataPaths.RootDirectory);
    }

    [Fact]
    public void ExplicitOverride_EmptyString_IsNeverAcceptedAsAnOverride()
    {
        NexoDataPaths.UseExplicitDataRoot(string.Empty);

        Assert.False(NexoDataPaths.IsUsingOverrideRoot);
    }

    [Fact]
    public void ExplicitOverride_WhitespaceOnly_IsNeverAcceptedAsAnOverride()
    {
        NexoDataPaths.UseExplicitDataRoot("   ");

        Assert.False(NexoDataPaths.IsUsingOverrideRoot);
    }

    [Fact]
    public void ExplicitOverride_TakesPrecedenceOverEnvironmentVariable()
    {
        Environment.SetEnvironmentVariable(EnvVar, CreateTempRootPath());
        var explicitRoot = CreateTempRootPath();

        NexoDataPaths.UseExplicitDataRoot(explicitRoot);

        Assert.Equal(Path.GetFullPath(explicitRoot), NexoDataPaths.RootDirectory);
    }

    [Fact]
    public void ExplicitOverride_DescribeActiveRoot_ReportsExplicitSource()
    {
        NexoDataPaths.UseExplicitDataRoot(CreateTempRootPath());

        var (_, source) = NexoDataPaths.DescribeActiveRoot();

        Assert.Equal("explícito (proceso)", source);
    }

    // ---------- Variable de entorno ----------

    [Fact]
    public void EnvironmentVariable_TakesEffectWhenNoExplicitOverride()
    {
        var envRoot = CreateTempRootPath();

        Environment.SetEnvironmentVariable(EnvVar, envRoot);

        Assert.True(NexoDataPaths.IsUsingOverrideRoot);
        Assert.Equal(Path.GetFullPath(envRoot), NexoDataPaths.RootDirectory);
    }

    [Fact]
    public void EnvironmentVariable_RelativePath_IsNormalizedToAbsolute()
    {
        var relative = "kohana-validation-" + Guid.NewGuid().ToString("N");

        Environment.SetEnvironmentVariable(EnvVar, relative);

        Assert.True(Path.IsPathRooted(NexoDataPaths.RootDirectory));
        Assert.Equal(Path.GetFullPath(relative), NexoDataPaths.RootDirectory);
    }

    [Fact]
    public void EnvironmentVariable_EmptyOrWhitespace_FallsBackToProduction()
    {
        Environment.SetEnvironmentVariable(EnvVar, "   ");

        Assert.False(NexoDataPaths.IsUsingOverrideRoot);
        Assert.Equal(Path.Combine(LocalApplicationData, "Sakura"), NexoDataPaths.RootDirectory);
    }

    [Fact]
    public void EnvironmentVariable_DescribeActiveRoot_ReportsTheVariableNameAsSource()
    {
        Environment.SetEnvironmentVariable(EnvVar, CreateTempRootPath());

        var (_, source) = NexoDataPaths.DescribeActiveRoot();

        Assert.Contains(EnvVar, source);
    }

    // ---------- Robustez ----------

    [Fact]
    public void InvalidOverridePath_DoesNotThrow_FallsBackInstead()
    {
        var invalid = "kohana-validation-\0-invalid";

        var exception = Record.Exception(() => NexoDataPaths.UseExplicitDataRoot(invalid));

        Assert.Null(exception);
        Assert.False(NexoDataPaths.IsUsingOverrideRoot);
    }

    // ---------- Aislamiento: colapso del legado, raíces compartidas, separación de perfiles ----------

    [Fact]
    public void OverrideActive_LegacyRootCollapsesToTheSameRoot()
    {
        NexoDataPaths.UseExplicitDataRoot(CreateTempRootPath());

        Assert.Equal(NexoDataPaths.RootDirectory, NexoDataPaths.LegacyRootDirectory);
    }

    [Fact]
    public void OverrideActive_AllDerivedStorePathsShareTheSameRoot()
    {
        NexoDataPaths.UseExplicitDataRoot(CreateTempRootPath());
        var root = NexoDataPaths.RootDirectory;

        Assert.StartsWith(root, NexoDataPaths.Tasks);
        Assert.StartsWith(root, NexoDataPaths.Focus);
        Assert.StartsWith(root, NexoDataPaths.Routines);
        Assert.StartsWith(root, NexoDataPaths.Settings);
        Assert.StartsWith(root, NexoDataPaths.Conversation);
        Assert.StartsWith(root, NexoDataPaths.LogsDirectory);
        Assert.StartsWith(root, NexoDataPaths.ModelsDirectory);
        Assert.StartsWith(root, NexoDataPaths.TempDirectory);
    }

    [Fact]
    public void TwoDifferentOverrides_ProduceTwoSeparateRoots()
    {
        NexoDataPaths.UseExplicitDataRoot(CreateTempRootPath());
        var firstRoot = NexoDataPaths.RootDirectory;

        NexoDataPaths.UseExplicitDataRoot(CreateTempRootPath());
        var secondRoot = NexoDataPaths.RootDirectory;

        Assert.NotEqual(firstRoot, secondRoot);
    }

    [Fact]
    public void OverrideActive_NeverResolvesToTheRealProductionOrLegacyFolders()
    {
        var realProductionRoot = Path.Combine(LocalApplicationData, "Sakura");
        var realLegacyRoot = Path.Combine(LocalApplicationData, "Nexo");

        NexoDataPaths.UseExplicitDataRoot(CreateTempRootPath());

        Assert.NotEqual(realProductionRoot, NexoDataPaths.RootDirectory);
        Assert.NotEqual(realLegacyRoot, NexoDataPaths.LegacyRootDirectory);
    }

    [Fact]
    public void OverrideActive_DoesNotCreateAnyDirectoryByItself()
    {
        var overrideRoot = CreateTempRootPath();

        NexoDataPaths.UseExplicitDataRoot(overrideRoot);

        // Fijar el override es solo resolución de rutas: no debe crear carpetas por adelantado.
        // Cada store sigue siendo responsable de crear la suya, de forma perezosa, al guardar.
        Assert.False(Directory.Exists(overrideRoot));
    }
}
