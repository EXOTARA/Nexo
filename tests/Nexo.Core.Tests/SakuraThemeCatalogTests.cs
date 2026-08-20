using Nexo.Core.Shell;
using Xunit;

namespace Nexo.Core.Tests;

public sealed class SakuraThemeCatalogTests
{
    [Fact]
    public void EveryThemeHasAUniqueIdAndAccent()
    {
        var ids = SakuraThemeCatalog.All.Select(theme => theme.Id).ToList();
        var accents = SakuraThemeCatalog.All.Select(theme => theme.AccentHex).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(accents.Count, accents.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void FindByAccentMatchesRegardlessOfCase()
    {
        var found = SakuraThemeCatalog.FindByAccent("#35c58a");

        Assert.NotNull(found);
        Assert.Equal("bosque", found!.Id);
    }

    [Fact]
    public void FindByAccentReturnsNullForAHandPickedColorOutsideTheGallery()
    {
        var found = SakuraThemeCatalog.FindByAccent("#123456");

        Assert.Null(found);
    }
}
