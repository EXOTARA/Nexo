using Nexo.Core.Automation;

namespace Nexo.Core.Tests;

public sealed class AutomationPermissionPolicyTests
{
    [Fact]
    public void OpenTerminal_IsSensitive()
    {
        var action = new AutomationAction
        {
            Type = AutomationActionType.OpenTerminal,
            WorkingDirectory = "C:\\Dev"
        };

        Assert.Equal(AutomationRiskLevel.Sensitive, AutomationPermissionPolicy.GetRisk(action));
    }

    [Fact]
    public void UnknownAction_IsBlocked()
    {
        var action = new AutomationAction { Type = AutomationActionType.None };

        Assert.False(AutomationPermissionPolicy.IsAllowed(action, out _));
        Assert.Equal(AutomationRiskLevel.Blocked, AutomationPermissionPolicy.GetRisk(action));
    }

    [Fact]
    public void InvalidVolume_IsRejected()
    {
        var action = new AutomationAction
        {
            Type = AutomationActionType.SetApplicationVolume,
            Target = "Spotify",
            NumericValue = 140
        };

        Assert.False(AutomationPermissionPolicy.IsAllowed(action, out _));
    }

    [Fact]
    public void SensitiveStep_ForcesConfirmation()
    {
        var routine = new RoutineDefinition
        {
            Name = "Programación",
            TriggerPhrase = "modo programación",
            RequiresConfirmation = false,
            Steps =
            [
                new AutomationAction
                {
                    Type = AutomationActionType.OpenTerminal,
                    WorkingDirectory = "C:\\Dev"
                }
            ]
        };

        Assert.True(AutomationPermissionPolicy.RequiresConfirmation(routine));
    }
}
