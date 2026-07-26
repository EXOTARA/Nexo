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
    public void PublicIdentity_UsesKohanaWithoutLosingLegacyName()
    {
        Assert.Equal("Kohana", ProductIdentity.ProductName);
        Assert.Equal("Nexo", ProductIdentity.PreviousProductName);
        Assert.Equal("Tu Windows, en flor.", ProductIdentity.Tagline);
        Assert.Equal("Oye Kohana", ProductIdentity.DefaultWakePhrase);
    }

    [Fact]
    public void DataPaths_SeparateCurrentAndLegacyIdentity()
    {
        Assert.Equal("Kohana", Path.GetFileName(NexoDataPaths.RootDirectory));
        Assert.Equal("Nexo", Path.GetFileName(NexoDataPaths.LegacyRootDirectory));
    }

    [Fact]
    public void Repository_RemainsOnNexoDuringTransition()
    {
        Assert.Equal("EXOTARA", ProductIdentity.RepositoryOwner);
        Assert.Equal("Nexo", ProductIdentity.RepositoryName);
        Assert.Equal("https://github.com/EXOTARA/Nexo", ProductIdentity.RepositoryUrl);
    }
}
