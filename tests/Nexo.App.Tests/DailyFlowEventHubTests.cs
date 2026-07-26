using Nexo.App.DailyFlow;

namespace Nexo.App.Tests;

/// <summary>
/// Diseño D3 — <see cref="DailyFlowEventHub"/> es solo reenvío de eventos, sin lógica de dominio;
/// se prueba como cualquier objeto C# puro, sin WPF.
/// </summary>
public sealed class DailyFlowEventHubTests
{
    [Fact]
    public void RaiseTasksChanged_InvokesOnlyTasksChanged()
    {
        var hub = new DailyFlowEventHub();
        var tasksRaised = 0;
        var focusRaised = 0;
        var routinesRaised = 0;
        hub.TasksChanged += (_, _) => tasksRaised++;
        hub.FocusChanged += (_, _) => focusRaised++;
        hub.RoutinesChanged += (_, _) => routinesRaised++;

        hub.RaiseTasksChanged();

        Assert.Equal(1, tasksRaised);
        Assert.Equal(0, focusRaised);
        Assert.Equal(0, routinesRaised);
    }

    [Fact]
    public void RaiseFocusChanged_InvokesOnlyFocusChanged()
    {
        var hub = new DailyFlowEventHub();
        var focusRaised = 0;
        hub.FocusChanged += (_, _) => focusRaised++;

        hub.RaiseFocusChanged();

        Assert.Equal(1, focusRaised);
    }

    [Fact]
    public void RaiseRoutinesChanged_InvokesOnlyRoutinesChanged()
    {
        var hub = new DailyFlowEventHub();
        var routinesRaised = 0;
        hub.RoutinesChanged += (_, _) => routinesRaised++;

        hub.RaiseRoutinesChanged();

        Assert.Equal(1, routinesRaised);
    }

    [Fact]
    public void RaisingWithoutSubscribers_DoesNotThrow()
    {
        var hub = new DailyFlowEventHub();

        var exception = Record.Exception(() =>
        {
            hub.RaiseTasksChanged();
            hub.RaiseFocusChanged();
            hub.RaiseRoutinesChanged();
        });

        Assert.Null(exception);
    }

    [Fact]
    public void SupportsMultipleSubscribersToTheSameEvent()
    {
        var hub = new DailyFlowEventHub();
        var first = 0;
        var second = 0;
        hub.TasksChanged += (_, _) => first++;
        hub.TasksChanged += (_, _) => second++;

        hub.RaiseTasksChanged();

        Assert.Equal(1, first);
        Assert.Equal(1, second);
    }
}
