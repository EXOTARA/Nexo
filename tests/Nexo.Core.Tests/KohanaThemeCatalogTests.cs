using Nexo.Core.Shell;
using Xunit;

namespace Nexo.Core.Tests;

public sealed class KohanaThemeCatalogTests
{
    [Fact]
    public void EveryThemeHasAUniqueIdAndAccent()
    {
        var ids = KohanaThemeCatalog.All.Select(theme => theme.Id).ToList();
        var accents = KohanaThemeCatalog.All.Select(theme => theme.AccentHex).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(accents.Count, accents.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void FindByAccentMatchesRegardlessOfCase()
    {
        var found = KohanaThemeCatalog.FindByAccent("#35c58a");

        Assert.NotNull(found);
        Assert.Equal("bosque", found!.Id);
    }

    [Fact]
    public void FindByAccentReturnsNullForAHandPickedColorOutsideTheGallery()
    {
        var found = KohanaThemeCatalog.FindByAccent("#123456");

        Assert.Null(found);
    }
}
