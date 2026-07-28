using Nexo.Core.Ambient;

namespace Nexo.Core.Tests;

public sealed class AmbientPermissionPolicyTests
{
    [Theory]
    [InlineData(AmbientAutonomyLevel.Ver)]
    [InlineData(AmbientAutonomyLevel.Guiar)]
    [InlineData(AmbientAutonomyLevel.Proponer)]
    public void IsAllowed_TrueForEveryLevelD4Defines(AmbientAutonomyLevel level)
    {
        var action = new AmbientQuickAction("id", "Etiqueta", level);

        Assert.True(AmbientPermissionPolicy.IsAllowed(action));
    }

    [Fact]
    public void IsAllowed_FailsClosedForUnrecognizedLevel()
    {
        var action = new AmbientQuickAction("id", "Etiqueta", (AmbientAutonomyLevel)99);

        Assert.False(AmbientPermissionPolicy.IsAllowed(action));
    }

    [Fact]
    public void RequiresConfirmation_ReflectsActionFlag()
    {
        var withConfirmation = new AmbientQuickAction(
            "id", "Etiqueta", AmbientAutonomyLevel.Proponer, RequiresConfirmation: true);
        var withoutConfirmation = new AmbientQuickAction(
            "id", "Etiqueta", AmbientAutonomyLevel.Ver);

        Assert.True(AmbientPermissionPolicy.RequiresConfirmation(withConfirmation));
        Assert.False(AmbientPermissionPolicy.RequiresConfirmation(withoutConfirmation));
    }
}
