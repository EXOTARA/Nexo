using Nexo.App.DailyFlow;
using Nexo.Core.Focus;

namespace Nexo.App.Tests;

/// <summary>
/// Diseño D3.1 — <see cref="FocusHistorySummaryBuilder"/> es lógica pura (sin WPF) sobre el
/// historial real de <see cref="FocusManager"/>. No inventa rachas, puntajes ni comparativas: solo
/// reporta lo que el historial ya contiene.
/// </summary>
public sealed class FocusHistorySummaryBuilderTests
{
    private static readonly DateTimeOffset ReferenceNow =
        new(2026, 7, 25, 10, 0, 0, TimeSpan.FromHours(-6));

    [Fact]
    public void EmptyHistory_ReturnsAnHonestEmptyState()
    {
        var summary = FocusHistorySummaryBuilder.Build([], ReferenceNow);

        Assert.False(summary.HasRecentSessions);
        Assert.False(summary.HasTopTask);
        Assert.Null(summary.TopTaskLabel);
        Assert.Equal(0, summary.TopTaskMinutesToday);
    }

    [Fact]
    public void RecentSessions_AreOrderedMostRecentFirst()
    {
        var history = new List<FocusHistoryEntry>
        {
            Entry("Primera", ReferenceNow.AddHours(-3), TimeSpan.FromMinutes(10)),
            Entry("Segunda", ReferenceNow.AddHours(-1), TimeSpan.FromMinutes(20)),
            Entry("Tercera", ReferenceNow, TimeSpan.FromMinutes(30))
        };

        var summary = FocusHistorySummaryBuilder.Build(history, ReferenceNow);

        Assert.Equal(
            ["Tercera", "Segunda", "Primera"],
            summary.RecentSessions.Select(session => session.Label));
    }

    [Fact]
    public void RecentSessions_AreLimitedToTheConfiguredMaximum()
    {
        var history = Enumerable.Range(0, FocusHistorySummaryBuilder.MaxRecentSessions + 4)
            .Select(index => Entry($"Sesión {index}", ReferenceNow.AddMinutes(-index), TimeSpan.FromMinutes(5)))
            .ToList();

        var summary = FocusHistorySummaryBuilder.Build(history, ReferenceNow);

        Assert.Equal(FocusHistorySummaryBuilder.MaxRecentSessions, summary.RecentSessions.Count);
    }

    [Fact]
    public void RecentSessions_IncludeTheRealDurationFormatted()
    {
        var history = new List<FocusHistoryEntry> { Entry("Enfoque", ReferenceNow, TimeSpan.FromMinutes(42)) };

        var summary = FocusHistorySummaryBuilder.Build(history, ReferenceNow);

        Assert.Equal("42 min", summary.RecentSessions.Single().DurationText);
    }

    [Fact]
    public void RecentSessions_FormatDurationsOverAnHour()
    {
        var history = new List<FocusHistoryEntry> { Entry("Enfoque", ReferenceNow, TimeSpan.FromMinutes(90)) };

        var summary = FocusHistorySummaryBuilder.Build(history, ReferenceNow);

        Assert.Equal("1 h 30 min", summary.RecentSessions.Single().DurationText);
    }

    [Fact]
    public void RecentSessions_ShowTheAssociatedTaskTitleWhenResolvable()
    {
        var taskId = Guid.NewGuid();
        var history = new List<FocusHistoryEntry>
        {
            Entry("Enfoque", ReferenceNow, TimeSpan.FromMinutes(10), taskId)
        };

        var summary = FocusHistorySummaryBuilder.Build(
            history, ReferenceNow, id => id == taskId ? "Escribir el informe" : null);

        Assert.Equal("Escribir el informe", summary.RecentSessions.Single().TaskTitle);
    }

    [Fact]
    public void RecentSessions_OmitTheTaskTitleWhenTheTaskWasDeleted()
    {
        var taskId = Guid.NewGuid();
        var history = new List<FocusHistoryEntry>
        {
            Entry("Enfoque", ReferenceNow, TimeSpan.FromMinutes(10), taskId)
        };

        var summary = FocusHistorySummaryBuilder.Build(history, ReferenceNow, _ => null);

        Assert.Null(summary.RecentSessions.Single().TaskTitle);
    }

    [Fact]
    public void RecentSessions_OmitTheTaskTitleWhenNoResolverWasProvided()
    {
        var taskId = Guid.NewGuid();
        var history = new List<FocusHistoryEntry>
        {
            Entry("Enfoque", ReferenceNow, TimeSpan.FromMinutes(10), taskId)
        };

        var summary = FocusHistorySummaryBuilder.Build(history, ReferenceNow);

        Assert.Null(summary.RecentSessions.Single().TaskTitle);
    }

    [Fact]
    public void TopTask_IsOnlyReportedWhenCalculableAndTheTaskStillExists()
    {
        var taskId = Guid.NewGuid();
        var history = new List<FocusHistoryEntry>
        {
            Entry("Enfoque", ReferenceNow, TimeSpan.FromMinutes(20), taskId),
            Entry("Enfoque", ReferenceNow.AddHours(-1), TimeSpan.FromMinutes(15), taskId)
        };

        var summary = FocusHistorySummaryBuilder.Build(
            history, ReferenceNow, id => id == taskId ? "Escribir el informe" : null);

        Assert.True(summary.HasTopTask);
        Assert.Equal("Escribir el informe", summary.TopTaskLabel);
        Assert.Equal(35, summary.TopTaskMinutesToday);
    }

    [Fact]
    public void TopTask_IsHiddenWhenTheTaskWasDeleted_InsteadOfShowingAnEmptyTitle()
    {
        var taskId = Guid.NewGuid();
        var history = new List<FocusHistoryEntry>
        {
            Entry("Enfoque", ReferenceNow, TimeSpan.FromMinutes(20), taskId)
        };

        var summary = FocusHistorySummaryBuilder.Build(history, ReferenceNow, _ => null);

        Assert.False(summary.HasTopTask);
        Assert.Null(summary.TopTaskLabel);
        Assert.Equal(0, summary.TopTaskMinutesToday);
    }

    [Fact]
    public void TopTask_IgnoresEntriesWithoutAnAssociatedTask()
    {
        var history = new List<FocusHistoryEntry>
        {
            Entry("Enfoque libre", ReferenceNow, TimeSpan.FromMinutes(60))
        };

        var summary = FocusHistorySummaryBuilder.Build(history, ReferenceNow, _ => "no debería usarse");

        Assert.False(summary.HasTopTask);
    }

    [Fact]
    public void TopTask_OnlyCountsEntriesCompletedToday()
    {
        var taskId = Guid.NewGuid();
        var history = new List<FocusHistoryEntry>
        {
            // Mismo día: cuenta.
            Entry("Enfoque", ReferenceNow, TimeSpan.FromMinutes(10), taskId),
            // Ayer: NO debe sumarse al total de "hoy".
            Entry("Enfoque", ReferenceNow.AddDays(-1), TimeSpan.FromMinutes(100), taskId)
        };

        var summary = FocusHistorySummaryBuilder.Build(
            history, ReferenceNow, id => id == taskId ? "Tarea" : null);

        Assert.Equal(10, summary.TopTaskMinutesToday);
    }

    [Fact]
    public void TopTask_PicksTheTaskWithTheMostMinutesAmongSeveral()
    {
        var winningTaskId = Guid.NewGuid();
        var otherTaskId = Guid.NewGuid();
        var history = new List<FocusHistoryEntry>
        {
            Entry("Enfoque", ReferenceNow, TimeSpan.FromMinutes(30), winningTaskId),
            Entry("Enfoque", ReferenceNow, TimeSpan.FromMinutes(10), otherTaskId)
        };

        var summary = FocusHistorySummaryBuilder.Build(
            history,
            ReferenceNow,
            id => id == winningTaskId ? "Tarea ganadora" : "Tarea perdedora");

        Assert.Equal("Tarea ganadora", summary.TopTaskLabel);
    }

    private static FocusHistoryEntry Entry(
        string label, DateTimeOffset completedAt, TimeSpan duration, Guid? taskId = null) => new()
    {
        Label = label,
        Kind = FocusSessionKind.Focus,
        StartedAt = completedAt - duration,
        CompletedAt = completedAt,
        Duration = duration,
        TaskId = taskId
    };
}
