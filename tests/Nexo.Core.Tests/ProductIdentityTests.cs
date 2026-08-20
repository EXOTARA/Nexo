using Nexo.Core.Branding;
using Nexo.Core.Diagnostics;

namespace Nexo.Core.Tests;

// Diseño D3.2 — comparte colección con NexoDataPathsTests: esa clase muta el override global de
// NexoDataPaths (limpiándolo antes/después de cada prueba), y esta clase asume que no hay ningún
// override activo. Sin este agrupamiento, xUnit podría ejecutarlas en paralelo y esta prueba
// podría leer una raíz de validación a mitad de otra prueba.
[Collection(NexoDataPathsCollection.Name)]
public sealed class ProductIdentityTests
{
    [Fact]
    public void PublicIdentity_UsesSakuraWithoutLosingLegacyName()
    {
        Assert.Equal("Sakura", ProductIdentity.ProductName);
        Assert.Equal("Kohana", ProductIdentity.PreviousProductName);
        Assert.Equal("Tu Windows, en flor.", ProductIdentity.Tagline);
        Assert.Equal("Oye Sakura", ProductIdentity.DefaultWakePhrase);
    }

    [Fact]
    public void DataPaths_SeparateCurrentAndLegacyIdentity()
    {
        Assert.Equal("Sakura", Path.GetFileName(NexoDataPaths.RootDirectory));

        // Diseño D70 — los nombres anteriores son dos y en orden: primero Kohana, luego Nexo, que
        // es donde siguen los modelos de voz de quien viene de la primera etapa.
        Assert.Equal("Kohana", Path.GetFileName(NexoDataPaths.LegacyRootDirectory));
        Assert.Equal(
            new[] { "Kohana", "Nexo" },
            NexoDataPaths.LegacyRootDirectories.Select(root => Path.GetFileName(root)!).ToArray());
    }

    [Fact]
    public void Repository_RemainsOnNexoDuringTransition()
    {
        Assert.Equal("EXOTARA", ProductIdentity.RepositoryOwner);
        Assert.Equal("Nexo", ProductIdentity.RepositoryName);
        Assert.Equal("https://github.com/EXOTARA/Nexo", ProductIdentity.RepositoryUrl);
    }
}
