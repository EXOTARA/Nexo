using Nexo.Core.Shell;
using Xunit;

namespace Nexo.Core.Tests;

public sealed class NavigationSelectionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnItemThatIsNotSelectedIsNeverMarked(bool expanded)
    {
        Assert.Equal(
            NavigationSelectionStyle.None,
            NavigationSelection.For(isSelected: false, railExpanded: expanded));
    }

    [Fact]
    public void CollapsedTheSelectedItemGlowsInsteadOfFilling()
    {
        // La regla del boceto. Con solo un icono, una pastilla rellena ocupa casi toda la franja y
        // deja de leerse como una pista para leerse como un bloque de color.
        Assert.Equal(
            NavigationSelectionStyle.Glow,
            NavigationSelection.For(isSelected: true, railExpanded: false));
    }

    [Fact]
    public void ExpandedTheSelectedItemGetsThePill()
    {
        // Con el nombre al lado, la pastilla abraza icono y texto y deja claro dónde acaba el
        // elemento activo cuando hay varios seguidos.
        Assert.Equal(
            NavigationSelectionStyle.Pill,
            NavigationSelection.For(isSelected: true, railExpanded: true));
    }

    [Fact]
    public void FoldingTheRailChangesHowTheSameSelectionLooks()
    {
        // El elemento activo no cambia al plegar el rail; lo que cambia es cómo se señala. Es el
        // motivo de que la regla dependa de los dos datos y no solo de cuál está activo.
        Assert.NotEqual(
            NavigationSelection.For(isSelected: true, railExpanded: true),
            NavigationSelection.For(isSelected: true, railExpanded: false));
    }
}
